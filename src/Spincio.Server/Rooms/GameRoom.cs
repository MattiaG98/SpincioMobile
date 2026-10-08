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
    private readonly List<string> _log = [];
    private readonly Task _processing;
    private BotMemory[] _memories = [];
    private Pcg32[] _rngs = [];
    private MatchState? _state;
    private ulong _seed;
    private string _salt = "";
    private ITimer? _moveTimer;
    private DateTimeOffset _turnDeadline;

    public GameRoom(
        string code, RoomOptions options, TimeProvider time, IRoomNotifier notifier, ILogger logger, Func<ulong>? newSeed = null)
    {
        Code = code;
        _options = options;
        _time = time;
        _notifier = notifier;
        _logger = logger;
        _newSeed = newSeed ?? (() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))));
        _bot = BotFactory.Create(options.BotLevel);
        LastActivity = time.GetUtcNow();
        _processing = Task.Run(ProcessAsync);
    }

    public string Code { get; }

    /// <summary>SHA-256(seed ‖ salt), public from the start of the match.</summary>
    public string? Commitment { get; private set; }

    public DateTimeOffset LastActivity { get; private set; }

    internal MatchState? State => _state;

    public Task<JoinResult> JoinAsync(string playerName, string connectionId) =>
        RunAsync(() => JoinCoreAsync(playerName, connectionId));

    public Task<JoinResult> RejoinAsync(string token, string connectionId) =>
        RunAsync(() => RejoinCoreAsync(token, connectionId));

    public Task<CommandResult> StartAsync(string token) => RunAsync(() => StartCoreAsync(token));

    public Task<CommandResult> PlayAsync(string token, int expectedSequence, string command) =>
        RunAsync(() => PlayCoreAsync(token, expectedSequence, command));

    public Task DisconnectedAsync(string connectionId) => RunAsync(() => DisconnectedCoreAsync(connectionId));

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

    private async Task<JoinResult> JoinCoreAsync(string playerName, string connectionId)
    {
        Touch();
        if (_state is not null)
        {
            return JoinResult.Fail("La partita è già iniziata.");
        }

        int index = JoinOrder.FirstOrDefault(i => _seats[i].Token is null && !_seats[i].IsBot, -1);
        if (index < 0)
        {
            return JoinResult.Fail("La stanza è piena.");
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
        _memories = [.. Seat.All.Select(s => new BotMemory(s))];
        _rngs = [.. Seat.All.Select(s => new Pcg32(_seed, 100UL + (ulong)s.Index))];
        _log.Clear();

        // The commitment goes out before anyone sees a card (it must not depend on the deal); then the deal, then
        // the room again, now started.
        await BroadcastRoomAsync();
        await AcceptAsync(SpincioEngine.NewMatch(_seed), command: null);
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
    private void ScheduleNext()
    {
        _moveTimer?.Dispose();
        var state = _state!;
        int sequence = state.Sequence;
        bool cpu = _seats[state.ToPlay.Index].PlayedByCpu;
        var delay = cpu ? _options.BotDelay : _options.TurnTimeout;
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
        await _notifier.Updated(connection, new GameUpdate(_state.Sequence, SpincioEngine.ViewFor(_state, seat), events, SecondsLeft(seat)));
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
            Commitment);
        foreach (var connection in Connections())
        {
            await _notifier.RoomChanged(connection, info);
        }
    }

    private MatchReveal Reveal() => new(_seed, _salt, [.. _log]);

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
