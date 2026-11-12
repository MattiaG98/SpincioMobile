using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// Search budget of <see cref="LookaheadBot"/>. Deterministic (positions counted, no clock), so bot matches stay
/// reproducible and the time per move is about the same on every device.
/// </summary>
public sealed record LookaheadOptions
{
    public static LookaheadOptions Default { get; } = new();

    /// <summary>Worlds of hidden cards sampled per decision, at most.</summary>
    public int Worlds { get; init; } = 16;

    /// <summary>Positions a decision may search: when each world is expensive, fewer worlds are sampled (0 = no limit).</summary>
    public long NodeBudget { get; init; } = 150_000;

    /// <summary>Worlds sampled even when the budget is spent.</summary>
    public int MinWorlds { get; init; } = 4;

    /// <summary>Longest search, in plays (a deal is 12 plays; the Spincione's first is 16).</summary>
    public int MaxPlies { get; init; } = 12;

    /// <summary>Plays searched past the end of the current deal: the first replies with the next hands.</summary>
    public int ExtraPlies { get; init; } = 1;

    /// <summary>Attempts per world to satisfy the declarations and silences of the current deal (<see cref="WorldSampler"/>).</summary>
    public int SamplingAttempts { get; init; } = 40;

    /// <summary>Reads silences: who played the first card of the deal without declaring had nothing to declare (A3).</summary>
    public bool InferSilence { get; init; } = true;

    /// <summary>Value of winning all 10 coins (F7) relative to one point.</summary>
    public double AllCoinsValue { get; init; } = 30;
}

/// <summary>
/// L3 (ADR 0019): reasons on the cards it has not seen. For each decision it deals them at random into possible
/// worlds, consistent with everything it knows (<see cref="WorldSampler"/>); in each world it tries every play and
/// searches the rest of the deal and the first reply of the next one, with our team playing its best and the opponents
/// theirs (<see cref="DealSearch"/>); it values what each team scores and captures by the round points it brings
/// (<see cref="RoundOutlook"/>), and plays the card that does best on average over the worlds.
/// </summary>
public sealed class LookaheadBot(LookaheadOptions? options = null) : IBot
{
    private readonly LookaheadOptions _options = options ?? LookaheadOptions.Default;
    private readonly WorldSampler _sampler = new(
        (options ?? LookaheadOptions.Default).SamplingAttempts, (options ?? LookaheadOptions.Default).InferSilence);
    private readonly GreedyBot _fallback = new();

    public string Name => "L3-Lookahead";

    public Command Choose(PlayerView view, BotMemory memory, ref Pcg32 rng)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(memory);

        var legal = SpincioEngine.LegalCommands(view);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"{view.Seat} has no legal command.");
        }

        var declare = legal.OfType<Declare>().FirstOrDefault();
        if (declare is not null)
        {
            return declare; // declaring is free (A3)
        }

        if (legal.Count == 1)
        {
            return legal[0];
        }

        var unseen = memory.Unseen().ToArray();
        if (!WorldSampler.Fits(view, unseen) || Values(view, memory, unseen, legal, ref rng) is not { } values)
        {
            return _fallback.Choose(view, memory, ref rng); // e.g. a memory started mid-round
        }

        double best = values.Max();
        var bestMoves = legal.Where((_, i) => values[i] >= best - 1e-9).ToList();
        return bestMoves[rng.NextInt(bestMoves.Count)];
    }

    /// <summary>Average value of each legal play over the sampled worlds, or null if a world does not fit the search.</summary>
    internal double[]? Values(PlayerView view, BotMemory memory, Card[] unseen, IReadOnlyList<Command> legal, ref Pcg32 rng)
    {
        int plies = Math.Min(view.HandCounts.Sum() + _options.ExtraPlies, _options.MaxPlies) - 1;
        var us = view.Seat.Team;
        var totals = new double[legal.Count];
        var search = new DealSearch(view.Variant, _options.AllCoinsValue);
        int worlds = _options.Worlds;
        for (int w = 0; w < worlds; w++)
        {
            var guess = _sampler.Sample(view, memory, unseen, ref rng);
            ulong worldSeed = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            if (!search.Load(SpincioEngine.Hypothetical(view, guess, worldSeed)))
            {
                return null;
            }

            for (int c = 0; c < legal.Count; c++)
            {
                totals[c] += search.Evaluate((PlayCard)legal[c], us, plies);
            }

            if (w == 0 && _options.NodeBudget > 0 && search.Nodes > 0)
            {
                // As many worlds as the budget allows at the cost of the first one.
                worlds = (int)Math.Clamp(_options.NodeBudget / search.Nodes, Math.Min(_options.MinWorlds, _options.Worlds), _options.Worlds);
            }
        }

        for (int c = 0; c < totals.Length; c++)
        {
            totals[c] /= worlds;
        }

        return totals;
    }
}
