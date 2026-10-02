using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// What the game page needs from a match, offline or online. It exposes only the viewer's
/// <see cref="PlayerView"/> and the events addressed to the viewer (CLAUDE.md rule 4).
/// </summary>
public interface IGameSession
{
    /// <summary>Raised after every visible change; the UI re-renders.</summary>
    event Action? Changed;

    bool IsStarted { get; }

    /// <summary>The viewer's seat (always 0 offline).</summary>
    Seat Me { get; }

    PlayerView View { get; }

    IReadOnlyList<Command> LegalCommands { get; }

    IReadOnlyList<string> Feed { get; }

    /// <summary>Set when a round ends, until the viewer closes the summary.</summary>
    RoundScored? PendingSummary { get; }

    MatchEnded? Result { get; }

    string? Error { get; }

    /// <summary>Waiting for someone else (CPU thinking or another player).</summary>
    bool IsWaiting { get; }

    /// <summary>Increments at every sweep, so the UI can replay the "Spazzino!" animation.</summary>
    int SweepCount { get; }

    /// <summary>One line for the footer (match number, CPU level, room code, fairness check).</summary>
    string Footer { get; }

    /// <summary>Whether "Nuova partita" is available at the end (offline only).</summary>
    bool CanRestart { get; }

    /// <summary>Plays the card animations of each move before it is shown; null = no animation (tests, server-side).</summary>
    IMoveAnimator? Animator { get; set; }

    string SeatName(Seat seat);

    Task PlayAsync(Command command);

    Task AcknowledgeSummaryAsync();

    /// <summary>Abandons (offline) or leaves (online) the match.</summary>
    Task LeaveAsync();
}

/// <summary>Holds the match the game page shows: the offline session unless an online one is active.</summary>
public sealed class GameHost(LocalGameSession local)
{
    private IGameSession? _online;

    public event Action? CurrentChanged;

    public LocalGameSession Local { get; } = local;

    public IGameSession Current => _online ?? Local;

    public void UseOnline(IGameSession session)
    {
        _online = session;
        CurrentChanged?.Invoke();
    }

    public void UseOffline()
    {
        _online = null;
        CurrentChanged?.Invoke();
    }
}
