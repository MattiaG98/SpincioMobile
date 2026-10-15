using Spincio.Bots;

namespace Spincio.Server.Rooms;

/// <summary>Room timings and CPU level (ADR 0004). Bound from the "Rooms" configuration section.</summary>
public sealed class RoomOptions
{
    /// <summary>Time a connected player has to move before a CPU plays for them once.</summary>
    public TimeSpan TurnTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Time a disconnected player has to come back before a CPU takes over the seat.</summary>
    public TimeSpan ReconnectGrace { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Pause before each CPU move, so humans can follow the game.</summary>
    public TimeSpan BotDelay { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>CPU level for empty seats; the server runs .NET natively, so L2 searches its full 16 worlds.</summary>
    public BotLevel BotLevel { get; set; } = BotLevel.Pimc;

    /// <summary>Finished or abandoned rooms are dropped after this idle time.</summary>
    public TimeSpan IdleLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Secret that seals the restore points given to players (ADR 0017). It must stay the same across restarts
    /// (on Render: generated once, <c>Rooms__CheckpointKey</c>); without it a room cannot be rebuilt after a restart.
    /// </summary>
    public string? CheckpointKey { get; set; }

    /// <summary>
    /// After a room is rebuilt, the next move waits at least this long, so the other players can come back with a
    /// possibly more recent restore point before the game moves on.
    /// </summary>
    public TimeSpan RestoreSettle { get; set; } = TimeSpan.FromSeconds(3);
}
