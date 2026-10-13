namespace Spincio.Server.Rooms;

/// <summary>
/// While people are playing, writes a line every few minutes. On a temporary host such as a GitHub Codespace (ADR 0014)
/// terminal output counts as activity: the machine stays on during a match and can switch itself off afterwards.
/// </summary>
public sealed partial class ActivityHeartbeat(RoomRegistry rooms, Lobby.LobbyRegistry lobby, TimeProvider time, ILogger<ActivityHeartbeat> log)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Beat();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>One heartbeat: a log line only if someone is connected. True if it wrote one.</summary>
    public bool Beat()
    {
        var (active, players) = rooms.Activity();
        int online = lobby.OnlineCount;
        if (players == 0 && online == 0)
        {
            return false;
        }

        LogActivity(log, active, players, online);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Partite in corso: {Rooms} stanze, {Players} giocatori collegati; {Online} con l'app aperta")]
    private static partial void LogActivity(ILogger logger, int rooms, int players, int online);
}
