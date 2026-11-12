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

    /// <summary>L2: Perfect Information Monte Carlo over sampled worlds with L1 rollouts.</summary>
    Pimc,

    /// <summary>L3: sampled worlds searched to the end of the deal with every seat playing its best (ADR 0019).</summary>
    Lookahead,
}

public static class BotFactory
{
    public static IBot Create(BotLevel level, PimcOptions? pimc = null, LookaheadOptions? lookahead = null) => level switch
    {
        BotLevel.Random => new RandomBot(),
        BotLevel.Greedy => new GreedyBot(),
        BotLevel.Pimc => new PimcBot(pimc),
        BotLevel.Lookahead => new LookaheadBot(lookahead),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
