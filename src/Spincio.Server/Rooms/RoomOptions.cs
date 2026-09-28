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

    public BotLevel BotLevel { get; set; } = BotLevel.Greedy;

    /// <summary>Finished or abandoned rooms are dropped after this idle time.</summary>
    public TimeSpan IdleLifetime { get; set; } = TimeSpan.FromHours(2);
}
