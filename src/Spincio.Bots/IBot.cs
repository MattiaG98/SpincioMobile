using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// A CPU player. It sees only what a human in the same seat would see: its <see cref="PlayerView"/>
/// and its memory of public events (CLAUDE.md rule 5). Randomness comes from the caller's PCG32 stream.
/// </summary>
public interface IBot
{
    string Name { get; }

    Command Choose(PlayerView view, BotMemory memory, ref Pcg32 rng);
}

public enum BotLevel
{
    /// <summary>L0: uniformly random legal command. For tests and fuzzing.</summary>
    Random,

    /// <summary>L1: one-ply greedy evaluation (MVP opponent).</summary>
    Greedy,
}

public static class BotFactory
{
    public static IBot Create(BotLevel level) => level switch
    {
        BotLevel.Random => new RandomBot(),
        BotLevel.Greedy => new GreedyBot(),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
