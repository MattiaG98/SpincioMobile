using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>Search budget of <see cref="PimcBot"/>. Deterministic (no clock), so bot matches stay reproducible.</summary>
public sealed record PimcOptions
{
    public static PimcOptions Default { get; } = new();

    /// <summary>Hidden-card worlds sampled per decision.</summary>
    public int Worlds { get; init; } = 16;

    /// <summary>Attempts per world to satisfy the declarations made in the current deal.</summary>
    public int SamplingAttempts { get; init; } = 40;

    /// <summary>Value of winning all 10 coins (F7) relative to one point.</summary>
    public double AllCoinsValue { get; init; } = 30;

    /// <summary>
    /// Weight of the L1 one-ply evaluation added to the sampled average (in points per L1 unit).
    /// Stabilises the choice when few worlds are sampled; 0 = pure PIMC.
    /// </summary>
    public double PriorWeight { get; init; } = 1.0;
}

/// <summary>
/// L2: Perfect Information Monte Carlo. For each decision it samples worlds of hidden cards consistent with
/// its memory and with the declarations made this deal, plays every candidate move in each world, finishes the
/// round with L1 for all four seats, and picks the move with the best average point difference for its team.
/// Partner play is covered implicitly: rollouts include the partner's moves (ADR 0007, ADR 0008).
/// </summary>
public sealed class PimcBot(PimcOptions? options = null, GreedyBot? rolloutPolicy = null) : IBot
{
    private readonly PimcOptions _options = options ?? PimcOptions.Default;
    private readonly GreedyBot _rollout = rolloutPolicy ?? new GreedyBot();

    public string Name => "L2-PIMC";

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
        int hiddenInHands = view.HandCounts.Where((_, i) => i != view.Seat.Index).Sum();
        if (unseen.Length != hiddenInHands + view.DeckCount)
        {
            // Memory does not match the view (e.g. a bot that joined mid-round): fall back to L1.
            return _rollout.Choose(view, memory, ref rng);
        }

        var totals = new double[legal.Count];
        for (int w = 0; w < _options.Worlds; w++)
        {
            var guess = SampleWorld(view, memory, unseen, ref rng);
            ulong worldSeed = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            var world = SpincioEngine.Hypothetical(view, guess, worldSeed);
            for (int c = 0; c < legal.Count; c++)
            {
                var rolloutRng = new Pcg32(worldSeed, 0); // common random numbers across candidates

                totals[c] += Rollout(world, legal[c], view.Seat.Team, memory, ref rolloutRng);
            }
        }

        var scores = new double[legal.Count];
        for (int c = 0; c < legal.Count; c++)
        {
            scores[c] = totals[c] / _options.Worlds;
            if (_options.PriorWeight != 0 && legal[c] is PlayCard play)
            {
                scores[c] += _options.PriorWeight * _rollout.Evaluate(play, view, memory);
            }
        }

        double best = scores.Max();
        var bestMoves = legal.Where((_, i) => scores[i] >= best - 1e-9).ToList();
        return bestMoves[rng.NextInt(bestMoves.Count)];
    }

    /// <summary>
    /// Deals the unseen cards to the other hands and the deck at random, retrying to honour the
    /// declarations (A1) other seats made in the current deal: their dealt hand is what they have
    /// already played this deal plus what we guess they still hold.
    /// </summary>
    internal HiddenGuess SampleWorld(PlayerView view, BotMemory memory, Card[] unseen, ref Pcg32 rng)
    {
        var declared = view.Declarations
            .Where(d => d.DealNumber == view.DealNumber && d.Seat != view.Seat)
            .ToList();

        HiddenGuess? guess = null;
        for (int attempt = 0; attempt < Math.Max(1, _options.SamplingAttempts); attempt++)
        {
            guess = Deal(view, memory, rng.Shuffle(unseen));
            if (declared.TrueForAll(d => Consistent(d, guess, memory)))
            {
                return guess;
            }
        }

        return guess!; // best effort: an unconstrained world
    }

    private static bool Consistent(DeclarationRecord declaration, HiddenGuess guess, BotMemory memory)
    {
        var dealtHand = memory.PlayedThisDeal(declaration.Seat).AddRange(guess.Hands[declaration.Seat.Index]);
        var value = Declarations.Evaluate(dealtHand);
        return value.Kind == declaration.Kind && value.Points == declaration.Points;
    }

    private static HiddenGuess Deal(PlayerView view, BotMemory memory, Card[] shuffled)
    {
        var hands = new ImmutableArray<Card>[Seat.Count];
        int position = 0;
        foreach (var seat in Seat.All)
        {
            if (seat == view.Seat)
            {
                hands[seat.Index] = view.Hand;
                continue;
            }

            int count = view.HandCounts[seat.Index];
            hands[seat.Index] = [.. shuffled.AsSpan(position, count)];
            position += count;
        }

        return new HiddenGuess(
            [.. hands],
            [.. shuffled.AsSpan(position)],
            [memory.CapturedBy(Team.A), memory.CapturedBy(Team.B)],
            memory.LastCapturingTeam);
    }

    /// <summary>Plays <paramref name="first"/>, then L1 for everyone until the round is scored.</summary>
    private double Rollout(MatchState world, Command first, Team us, BotMemory memory, ref Pcg32 rng)
    {
        var start = world.Score;
        var step = SpincioEngine.Apply(world, first).Value;

        // Rollout memories: public knowledge shared by all, plus each seat's own hand in this world.
        var memories = Seat.All.Select(s => RolloutMemory(s, world, memory)).ToArray();
        foreach (var m in memories)
        {
            m.Observe(step.EventsFor(m.Seat));
        }

        while (true)
        {
            if (step.Events.OfType<RoundScored>().FirstOrDefault() is { } scored)
            {
                return Value(scored, start, us);
            }

            var state = step.State;
            var seat = state.ToPlay;
            var command = _rollout.Choose(SpincioEngine.ViewFor(state, seat), memories[seat.Index], ref rng);
            step = SpincioEngine.Apply(state, command).Value;
            foreach (var m in memories)
            {
                m.Observe(step.EventsFor(m.Seat));
            }
        }
    }

    private double Value(RoundScored scored, TeamScores start, Team us)
    {
        var them = us == Team.A ? Team.B : Team.A;
        double value = (scored.MatchScore.For(us) - start.For(us)) - (scored.MatchScore.For(them) - start.For(them));
        if (scored.Score.AllCoinsTeam is { } coins)
        {
            value += coins == us ? _options.AllCoinsValue : -_options.AllCoinsValue;
        }

        return value;
    }

    /// <summary>
    /// A rollout seat knows what the real bot publicly knows (table, plays, captures) plus its own hand in the
    /// sampled world. Built by replaying synthetic events so <see cref="BotMemory"/> stays the single source of truth.
    /// </summary>
    private static BotMemory RolloutMemory(Seat seat, MatchState world, BotMemory real)
    {
        var memory = new BotMemory(seat);
        var ownHand = world.HandOf(real.Seat);
        var publicCards = real.Seen.Where(c => !ownHand.Contains(c)); // everything seen except our private hand
        memory.Observe(new HandDealt(seat, [.. world.HandOf(seat)]));
        memory.Observe(new DealStarted(1, [.. publicCards], world.Deck.Length));
        return memory;
    }
}
