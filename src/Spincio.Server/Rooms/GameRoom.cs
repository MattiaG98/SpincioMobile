using System.Security.Cryptography;
using System.Threading.Channels;
using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Server.Rooms;

/// <summary>
/// One online match (ADR 0004). The server is authoritative: clients send intentions, the room validates them
/// with the engine and sends each seat only its own view and the events addressed to it. All work runs one item
/// at a time through a queue, so there are no races between players, CPU moves and timers.
/// </summary>
public sealed partial class GameRoom : IAsyncDisposable
{
    /// <summary>The first two people to join sit opposite each other, as partners.</summary>
    private static readonly int[] JoinOrder = [0, 2, 1, 3];

    private readonly SeatSlot[] _seats = [new(), new(), new(), new()];
    private readonly Channel<Func<Task>> _queue =
        Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });

    private readonly RoomOptions _options;
    private readonly TimeProvider _time;
    private readonly IRoomNotifier _notifier;
    private readonly ILogger _logger;
    private readonly Func<ulong> _newSeed;
    private readonly IBot _bot;
    private readonly CheckpointProtector _checkpoints;
    private readonly List<string> _log = [];
    private readonly Task _processing;
    private BotMemory[] _memories = [];
    private Pcg32[] _rngs = [];
    private MatchState? _state;
    private ulong _seed;
    private string _salt = "";
    private ITimer? _moveTimer;
    private DateTimeOffset _turnDeadline;
    private string? _checkpoint;

    public GameRoom(
        string code,
        RoomOptions options,
        TimeProvider time,
        IRoomNotifier notifier,
        ILogger logger,
        Func<ulong>? newSeed = null,
        CheckpointProtector? checkpoints = null,
        GameVariant variant = GameVariant.Spincio)
    {
        Variant = variant;
        Code = code;
        _options = options;
        _time = time;
        _notifier = notifier;
        _logger = logger;
        _newSeed = newSeed ?? (() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))));
        _bot = BotFactory.Create(options.BotLevel);
        _checkpoints = checkpoints ?? new CheckpointProtector(options.CheckpointKey);
        LastActivity = time.GetUtcNow();
        _processing = Task.Run(ProcessAsync);
    }

    public string Code { get; }

    /// <summary>The game mode of the room (SPEC §1.7).</summary>
    public GameVariant Variant { get; private set; }

    /// <summary>SHA-256(seed ‖ salt), public from the start of the match.</summary>
    public string? Commitment { get; private set; }

    public DateTimeOffset LastActivity { get; private set; }

    internal MatchState? State => _state;

    public Task<JoinResult> JoinAsync(string playerName, string connectionId) =>
        RunAsync(() => JoinCoreAsync(playerName, connectionId, JoinOrder));

    /// <summary>
    /// Joins on the same team as <paramref name="inviterSeat"/> (<paramref name="sameTeam"/>) or on the other one
    /// (ADR 0015): the first free seat of that team.
    /// </summary>
    public Task<JoinResult> JoinNearAsync(string playerName, string connectionId, int inviterSeat, bool sameTeam)
    {
        if (inviterSeat is < 0 or >= 4)
        {
            return Task.FromResult(JoinResult.Fail("Posto non valido."));
        }

        int[] order = [.. JoinOrder.Where(i => i != inviterSeat && (i % 2 == inviterSeat % 2) == sameTeam)];
        return RunAsync(() => JoinCoreAsync(playerName, connectionId, order, sameTeam ? "Nella squadra di chi ti ha invitato non c'è più posto." : "Nella squadra avversaria non c'è più posto."));
    }

    /// <summary>The seat and name of the player holding <paramref name="token"/>, and the free seats, before the match starts.</summary>
    public Task<SeatSnapshot?> SnapshotForAsync(string token) => RunAsync(() =>
    {
        int index = SeatOf(token);
        SeatSnapshot? snapshot = index < 0 || _state is not null
            ? null
            : new SeatSnapshot(index, _seats[index].Name, [.. Enumerable.Range(0, 4).Where(i => _seats[i].Token is null && !_seats[i].IsBot)])
            {
                Variant = Variant,
            };
        return Task.FromResult(snapshot);
    });

    public Task<JoinResult> RejoinAsync(string token, string connectionId) =>
        RunAsync(() => RejoinCoreAsync(token, connectionId));

    public Task<CommandResult> StartAsync(string token) => RunAsync(() => StartCoreAsync(token));

    public Task<CommandResult> PlayAsync(string token, int expectedSequence, string command) =>
        RunAsync(() => PlayCoreAsync(token, expectedSequence, command));

    public Task DisconnectedAsync(string connectionId) => RunAsync(() => DisconnectedCoreAsync(connectionId));

    /// <summary>
    /// Rebuilds a started match from a restore point (ADR 0017), in a new room with the same code, after the server
    /// restarted. Everyone is away until they rejoin with their token. False if the checkpoint does not replay.
    /// </summary>
    public Task<bool> RestoreAsync(RoomCheckpoint checkpoint) => RunAsync(() => Task.FromResult(RestoreCore(checkpoint)));

    /// <summary>
    /// Brings a rebuilt room forward with a more recent restore point of the same match, as long as nothing
    /// different has been played in the meantime. True if moves were added.
    /// </summary>
    public Task<bool> ExtendAsync(RoomCheckpoint checkpoint) => RunAsync(() => ExtendCoreAsync(checkpoint));

    /// <summary>Completes once everything queued so far has run (used by tests).</summary>
    public Task WhenIdleAsync() => RunAsync(() => Task.FromResult(true));

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _processing.ConfigureAwait(false);
        _moveTimer?.Dispose();
        foreach (var slot in _seats)
        {
            slot.GraceTimer?.Dispose();
        }
    }

    private async Task<JoinResult> JoinCoreAsync(string playerName, string connectionId, int[] order, string full = "La stanza è piena.")
    {
        Touch();
        if (_state is not null)
        {
            return JoinResult.Fail("La partita è già iniziata.");
        }

        int index = order.FirstOrDefault(i => _seats[i].Token is null && !_seats[i].IsBot, -1);
        if (index < 0)
        {
            return JoinResult.Fail(full);
        }

        var slot = _seats[index];
        slot.Name = CleanName(playerName, index);
        slot.Token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        slot.ConnectionId = connectionId;
        await BroadcastRoomAsync();
        return new JoinResult(true, null, Code, index, slot.Token);
    }

    private async Task<JoinResult> RejoinCoreAsync(string token, string connectionId)
    {
        Touch();
        int index = SeatOf(token);
        if (index < 0)
        {
            return JoinResult.Fail("Posto non trovato.");
        }

        var slot = _seats[index];
        slot.ConnectionId = connectionId;
        slot.GraceTimer?.Dispose();
        slot.GraceTimer = null;
        bool wasTakenOver = slot.BotControlled;
        slot.BotControlled = false;
        await BroadcastRoomAsync();

        if (_state is not null)
        {
            if (wasTakenOver && _state.Phase == MatchPhase.AwaitingPlay && _state.ToPlay.Index == index)
            {
                ScheduleNext(); // the human is back in control of the current turn
            }

            await SendAsync(index, events: []);
            if (_state.Phase == MatchPhase.MatchOver)
            {
                await _notifier.Revealed(connectionId, Reveal());
            }
        }

        return new JoinResult(true, null, Code, index, token);
    }

    private async Task<CommandResult> StartCoreAsync(string token)
    {
        Touch();
        if (SeatOf(token) < 0)
        {
            return CommandResult.Fail("Posto non trovato.");
        }

        if (_state is not null)
        {
            return CommandResult.Fail("La partita è già iniziata.");
        }

        foreach (var slot in _seats.Where(s => s.Token is null))
        {
            slot.IsBot = true;
            slot.Name = "CPU";
        }

        _seed = _newSeed();
        _salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        Commitment = Contracts.Commitment.Of(_seed, _salt);
        _memories = [.. Seat.All.Select(s => new BotMemory(s, Variant))];
        _rngs = [.. Seat.All.Select(s => new Pcg32(_seed, 100UL + (ulong)s.Index))];
        _log.Clear();

        // The commitment goes out before anyone sees a card (it must not depend on the deal); then the deal, then
        // the room again, now started.
        await BroadcastRoomAsync();
        await AcceptAsync(SpincioEngine.NewMatch(_seed, variant: Variant), command: null);
        await BroadcastRoomAsync();
        return CommandResult.Success;
    }

    private async Task<CommandResult> PlayCoreAsync(string token, int expectedSequence, string text)
    {
        Touch();
        int index = SeatOf(token);
        if (index < 0)
        {
            return CommandResult.Fail("Posto non trovato.");
        }

        if (_state is null)
        {
            return CommandResult.Fail("La partita non è ancora iniziata.");
        }

        if (expectedSequence != _state.Sequence)
        {
            return CommandResult.Fail("La partita è andata avanti: mossa non applicata.");
        }

        if (!CommandCodec.TryDecode(text, out var command) || command!.Seat.Index != index)
        {
            return CommandResult.Fail("Comando non valido.");
        }

        var result = SpincioEngine.Apply(_state, command);
        if (!result.IsSuccess)
        {
            return CommandResult.Fail($"Mossa non valida: {result.Error}");
        }

        await AcceptAsync(result.Value, text);
        return CommandResult.Success;
    }

    private async Task<bool> DisconnectedCoreAsync(string connectionId)
    {
        int index = Array.FindIndex(_seats, s => s.ConnectionId == connectionId);
        if (index < 0)
        {
            return false;
        }

        var slot = _seats[index];
        slot.ConnectionId = null;
        if (_state is null)
        {
            // Leaving the lobby frees the seat.
            slot.Token = null;
            slot.Name = "";
        }
        else if (_state.Phase == MatchPhase.AwaitingPlay)
        {
            slot.GraceTimer?.Dispose();
            slot.GraceTimer = _time.CreateTimer(
                _ => _ = RunAsync(() => TakeOverAsync(index)), null, _options.ReconnectGrace, Timeout.InfiniteTimeSpan);
        }

        await BroadcastRoomAsync();
        return true;
    }

    /// <summary>The grace period is over: a CPU plays for the absent player until they come back.</summary>
    private async Task<bool> TakeOverAsync(int index)
    {
        var slot = _seats[index];
        slot.GraceTimer?.Dispose();
        slot.GraceTimer = null;
        if (slot.Connected || _state is not { Phase: MatchPhase.AwaitingPlay } state)
        {
            return false;
        }

        slot.BotControlled = true;
        if (state.ToPlay.Index == index)
        {
            ScheduleNext();
        }

        await BroadcastRoomAsync();
        return true;
    }

    /// <summary>A CPU move: for a CPU seat, a taken-over seat, or a player whose turn timed out.</summary>
    private async Task<bool> CpuMoveAsync(int sequence)
    {
        if (_state is not { Phase: MatchPhase.AwaitingPlay } state || state.Sequence != sequence)
        {
            return false; // stale timer: the game has moved on
        }

        var seat = state.ToPlay;
        var command = _bot.Choose(SpincioEngine.ViewFor(state, seat), _memories[seat.Index], ref _rngs[seat.Index]);
        await AcceptAsync(SpincioEngine.Apply(state, command).Value, CommandCodec.Encode(command));
        return true;
    }

    private async Task AcceptAsync(Transition transition, string? command)
    {
        _state = transition.State;
        if (command is not null)
        {
            _log.Add(command);
        }

        foreach (var memory in _memories)
        {
            memory.Observe(transition.EventsFor(memory.Seat));
        }

        _checkpoint = _checkpoints.Protect(CheckpointOf());
        if (_state.Phase == MatchPhase.MatchOver)
        {
            _moveTimer?.Dispose();
            _moveTimer = null;
        }
        else
        {
            ScheduleNext();
        }

        for (int i = 0; i < Seat.Count; i++)
        {
            await SendAsync(i, [.. transition.EventsFor(new Seat(i))]);
        }

        if (_state.Phase == MatchPhase.MatchOver)
        {
            var reveal = Reveal();
            foreach (var connection in Connections())
            {
                await _notifier.Revealed(connection, reveal);
            }
        }
    }

    /// <summary>Arms the timer for whoever plays next: a short pause for CPUs, the turn timeout for people.</summary>
    private void ScheduleNext(TimeSpan atLeast = default)
    {
        _moveTimer?.Dispose();
        var state = _state!;
        int sequence = state.Sequence;
        bool cpu = _seats[state.ToPlay.Index].PlayedByCpu;
        var delay = cpu ? _options.BotDelay : _options.TurnTimeout;
        if (delay < atLeast)
        {
            delay = atLeast;
        }

        _turnDeadline = _time.GetUtcNow() + delay;
        _moveTimer = _time.CreateTimer(
            _ => _ = RunAsync(() => CpuMoveAsync(sequence)), null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task SendAsync(int index, IReadOnlyList<GameEvent> events)
    {
        if (_seats[index].ConnectionId is not { } connection || _state is null)
        {
            return;
        }

        var seat = new Seat(index);
        var update = new GameUpdate(_state.Sequence, SpincioEngine.ViewFor(_state, seat), events, SecondsLeft(seat)) { Checkpoint = _checkpoint };
        await _notifier.Updated(connection, update);
    }

    private int SecondsLeft(Seat seat)
    {
        if (_state is not { Phase: MatchPhase.AwaitingPlay } state || state.ToPlay != seat || _seats[seat.Index].PlayedByCpu)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Ceiling((_turnDeadline - _time.GetUtcNow()).TotalSeconds));
    }

    private async Task BroadcastRoomAsync()
    {
        var info = new RoomInfo(
            Code,
            [.. _seats.Select((s, i) => new SeatInfo(i, s.Name, s.PlayedByCpu, s.IsBot || s.Connected))],
            _state is not null,
            Commitment)
        {
            Variant = Variant,
        };
        foreach (var connection in Connections())
        {
            await _notifier.RoomChanged(connection, info);
        }
    }

    private MatchReveal Reveal() => new(_seed, _salt, [.. _log]) { Variant = Variant };

    private RoomCheckpoint CheckpointOf() =>
        new(Code, _seed, _salt, [.. _seats.Select(s => new CheckpointSeat(s.Name, s.Token, s.IsBot))], [.. _log], Variant);

    private bool RestoreCore(RoomCheckpoint checkpoint)
    {
        if (_state is not null || checkpoint.Seats.Count != Seat.Count || !string.Equals(checkpoint.Code, Code, StringComparison.Ordinal))
        {
            return false;
        }

        var transition = SpincioEngine.NewMatch(checkpoint.Seed, variant: checkpoint.Variant);
        var state = transition.State;
        var memories = Seat.All.Select(s => new BotMemory(s, checkpoint.Variant)).ToArray();
        Observe(memories, transition);
        foreach (string text in checkpoint.Log)
        {
            if (!CommandCodec.TryDecode(text, out var command) || SpincioEngine.Apply(state, command!) is not { IsSuccess: true } step)
            {
                return false;
            }

            state = step.Value.State;
            Observe(memories, step.Value);
        }

        for (int i = 0; i < Seat.Count; i++)
        {
            var slot = _seats[i];
            slot.Name = checkpoint.Seats[i].Name;
            slot.Token = checkpoint.Seats[i].Token;
            slot.IsBot = checkpoint.Seats[i].IsBot;
            slot.ConnectionId = null;
        }

        Variant = checkpoint.Variant;
        _seed = checkpoint.Seed;
        _salt = checkpoint.Salt;
        Commitment = Contracts.Commitment.Of(_seed, _salt);
        _memories = memories;
        _rngs = [.. Seat.All.Select(s => new Pcg32(_seed, 100UL + (ulong)s.Index))];
        _log.Clear();
        _log.AddRange(checkpoint.Log);
        _state = state;
        _checkpoint = _checkpoints.Protect(CheckpointOf());
        Touch();

        if (state.Phase == MatchPhase.AwaitingPlay)
        {
            // People who do not come back are replaced by a CPU after the usual grace period.
            for (int i = 0; i < Seat.Count; i++)
            {
                if (!_seats[i].IsBot)
                {
                    int index = i;
                    _seats[i].GraceTimer = _time.CreateTimer(
                        _ => _ = RunAsync(() => TakeOverAsync(index)), null, _options.ReconnectGrace, Timeout.InfiniteTimeSpan);
                }
            }

            ScheduleNext(_options.RestoreSettle);
        }

        return true;
    }

    private async Task<bool> ExtendCoreAsync(RoomCheckpoint checkpoint)
    {
        if (_state is null || checkpoint.Seed != _seed || checkpoint.Log.Count <= _log.Count || !checkpoint.Log.Take(_log.Count).SequenceEqual(_log))
        {
            return false;
        }

        foreach (string text in checkpoint.Log.Skip(_log.Count).ToList())
        {
            if (!CommandCodec.TryDecode(text, out var command) || SpincioEngine.Apply(_state, command!) is not { IsSuccess: true } step)
            {
                return false;
            }

            await AcceptAsync(step.Value, text);
        }

        return true;
    }

    private static void Observe(BotMemory[] memories, Transition transition)
    {
        foreach (var memory in memories)
        {
            memory.Observe(transition.EventsFor(memory.Seat));
        }
    }

    private List<string> Connections() =>
        _seats.Select(s => s.ConnectionId).OfType<string>().ToList();

    private int SeatOf(string token) =>
        string.IsNullOrEmpty(token) ? -1 : Array.FindIndex(_seats, s => s.Token == token);

    private void Touch() => LastActivity = _time.GetUtcNow();

    private static string CleanName(string name, int index)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > 16)
        {
            clean = clean[..16];
        }

        return clean.Length == 0 ? $"Giocatore {index + 1}" : clean;
    }

    private Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool queued = _queue.Writer.TryWrite(async () =>
        {
            try
            {
                done.SetResult(await work());
            }
            catch (Exception e)
            {
                done.SetException(e);
            }
        });
        if (!queued)
        {
            done.SetException(new ObjectDisposedException(nameof(GameRoom)));
        }

        return done.Task;
    }

    private async Task ProcessAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await work();
            }
            catch (Exception e)
            {
                LogWorkFailed(_logger, e, Code);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Room {Code}: work item failed")]
    private static partial void LogWorkFailed(ILogger logger, Exception exception, string code);

    private sealed class SeatSlot
    {
        public string Name { get; set; } = "";

        /// <summary>Secret that identifies the person in this seat; null for empty and CPU seats.</summary>
        public string? Token { get; set; }

        public string? ConnectionId { get; set; }

        /// <summary>A CPU from the start of the match.</summary>
        public bool IsBot { get; set; }

        /// <summary>A person's seat currently played by a CPU after a disconnection.</summary>
        public bool BotControlled { get; set; }

        public ITimer? GraceTimer { get; set; }

        public bool Connected => ConnectionId is not null;

        public bool PlayedByCpu => IsBot || BotControlled;
    }
}

/// <summary>Who is inviting (seat and name) and which seats are still free, for an invitation (ADR 0015).</summary>
public sealed record SeatSnapshot(int Seat, string Name, IReadOnlyList<int> FreeSeats)
{
    public GameVariant Variant { get; init; }

    public bool CanJoinWith => FreeSeats.Any(s => s != Seat && s % 2 == Seat % 2);

    public bool CanJoinAgainst => FreeSeats.Any(s => s % 2 != Seat % 2);
}
