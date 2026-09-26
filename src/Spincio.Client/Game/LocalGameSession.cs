using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// Offline 2v2 match: the human at seat 0, three L1 bots. This class holds the authoritative
/// <see cref="MatchState"/>; the UI only ever gets the human's <see cref="PlayerView"/> and the events
/// addressed to the human (CLAUDE.md rule 4). Every accepted command is saved (seed + log, ADR 0003).
/// </summary>
public sealed class LocalGameSession
{
    public const string RulesVersion = "1.2";
    public const int FeedLength = 8;
    public static readonly Seat Human = new(0);
    public static readonly TimeSpan DefaultCpuDelay = TimeSpan.FromMilliseconds(700);

    private readonly ISavedGameStore _store;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly TimeSpan _cpuDelay;
    private readonly IBot _bot;
    private readonly List<string> _feed = [];
    private readonly List<string> _commands = [];
    private BotMemory[] _memories = [];
    private Pcg32[] _rngs = [];
    private MatchState? _state;
    private bool _cpuLoopRunning;

    public LocalGameSession(ISavedGameStore store, Func<TimeSpan, Task>? delay = null, TimeSpan? cpuDelay = null, IBot? bot = null)
    {
        _store = store;
        _delay = delay ?? Task.Delay;
        _cpuDelay = cpuDelay ?? DefaultCpuDelay;
        _bot = bot ?? new GreedyBot();
    }

    /// <summary>Raised after every visible change; the UI re-renders.</summary>
    public event Action? Changed;

    public bool IsStarted => _state is not null;

    public ulong Seed { get; private set; }

    public PlayerView View => SpincioEngine.ViewFor(State, Human);

    public IReadOnlyList<Command> LegalCommands => SpincioEngine.LegalCommands(View);

    public IReadOnlyList<string> Feed => _feed;

    /// <summary>Set when a round ends; bots wait until the human closes the summary.</summary>
    public RoundScored? PendingSummary { get; private set; }

    public MatchEnded? Result { get; private set; }

    public string? Error { get; private set; }

    public bool IsCpuThinking => _cpuLoopRunning;

    private MatchState State => _state ?? throw new InvalidOperationException("No match in progress.");

    public async Task NewGameAsync(ulong seed)
    {
        Reset(seed);
        Accept(SpincioEngine.NewMatch(seed), command: null);
        await SaveAsync();
        await RunCpusAsync();
    }

    public async Task<bool> HasSavedGameAsync() =>
        SavedGame.FromJson(await _store.LoadAsync()) is { } saved && saved.RulesVersion == RulesVersion;

    /// <summary>Restores the saved match by replaying its command log. False if there is none or it is unusable.</summary>
    public async Task<bool> TryResumeAsync()
    {
        var saved = SavedGame.FromJson(await _store.LoadAsync());
        if (saved is null || saved.RulesVersion != RulesVersion)
        {
            return false;
        }

        try
        {
            Reset(saved.Seed);
            Accept(SpincioEngine.NewMatch(saved.Seed), command: null);
            foreach (var text in saved.Commands)
            {
                var result = SpincioEngine.Apply(State, CommandCodec.Decode(text));
                if (!result.IsSuccess)
                {
                    return false;
                }

                Accept(result.Value, text);
            }
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return false;
        }

        PendingSummary = null; // do not re-show a summary the player may already have closed
        if (State.Phase == MatchPhase.MatchOver)
        {
            return false;
        }

        Changed?.Invoke();
        await RunCpusAsync();
        return true;
    }

    public async Task PlayAsync(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Seat != Human || _cpuLoopRunning || PendingSummary is not null)
        {
            return;
        }

        var result = SpincioEngine.Apply(State, command);
        if (!result.IsSuccess)
        {
            Error = $"Mossa non valida: {result.Error}";
            Changed?.Invoke();
            return;
        }

        Error = null;
        Accept(result.Value, CommandCodec.Encode(command));
        await SaveAsync();
        Changed?.Invoke();
        await RunCpusAsync();
    }

    public async Task AcknowledgeSummaryAsync()
    {
        PendingSummary = null;
        Changed?.Invoke();
        await RunCpusAsync();
    }

    public async Task AbandonAsync()
    {
        await _store.ClearAsync();
        _state = null;
        Changed?.Invoke();
    }

    private void Reset(ulong seed)
    {
        Seed = seed;
        _feed.Clear();
        _commands.Clear();
        _memories = [.. Seat.All.Select(s => new BotMemory(s))];
        _rngs = [.. Seat.All.Select(s => new Pcg32(seed, 100UL + (ulong)s.Index))];
        PendingSummary = null;
        Result = null;
        Error = null;
    }

    private void Accept(Transition transition, string? command)
    {
        _state = transition.State;
        if (command is not null)
        {
            _commands.Add(command);
        }

        foreach (var memory in _memories)
        {
            memory.Observe(transition.EventsFor(memory.Seat));
        }

        foreach (var e in transition.EventsFor(Human))
        {
            if (GameText.Describe(e) is { } line)
            {
                _feed.Add(line);
                if (_feed.Count > FeedLength)
                {
                    _feed.RemoveAt(0);
                }
            }

            switch (e)
            {
                case RoundScored scored:
                    PendingSummary = scored;
                    break;
                case MatchEnded ended:
                    Result = ended;
                    break;
            }
        }
    }

    private async Task RunCpusAsync()
    {
        if (_cpuLoopRunning)
        {
            return;
        }

        _cpuLoopRunning = true;
        try
        {
            while (_state is { Phase: MatchPhase.AwaitingPlay } state && state.ToPlay != Human && PendingSummary is null)
            {
                Changed?.Invoke();
                await _delay(_cpuDelay);

                var seat = state.ToPlay;
                var command = _bot.Choose(SpincioEngine.ViewFor(state, seat), _memories[seat.Index], ref _rngs[seat.Index]);
                Accept(SpincioEngine.Apply(state, command).Value, CommandCodec.Encode(command));
                await SaveAsync();
            }
        }
        finally
        {
            _cpuLoopRunning = false;
        }

        if (_state?.Phase == MatchPhase.MatchOver)
        {
            await _store.ClearAsync();
        }

        Changed?.Invoke();
    }

    private Task SaveAsync() => _store.SaveAsync(new SavedGame(RulesVersion, Seed, [.. _commands]).ToJson());
}
