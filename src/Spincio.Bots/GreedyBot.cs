using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>Tunable weights of <see cref="GreedyBot"/>, in "expected card" units.</summary>
public sealed record GreedyWeights
{
    public static GreedyWeights Default { get; } = new();

    public double Card { get; init; } = 1.0;

    public double Coin { get; init; } = 1.0;

    public double Seven { get; init; } = 1.5;

    public double Settebello { get; init; } = 5.0;

    public double Rebello { get; init; } = 4.0;

    /// <summary>Extra for low coins (A, 2, 3), the start of a spincio (scala di denari).</summary>
    public double SpincioStart { get; init; } = 0.5;

    public double Sweep { get; init; } = 10.0;

    /// <summary>Penalty × probability that the next opponent can sweep the table we leave.</summary>
    public double SweepRisk { get; init; } = 8.0;

    /// <summary>Fraction of a dropped card's value we consider given away.</summary>
    public double DropCost { get; init; } = 0.3;

    /// <summary>Spincione: a capture all of one suit is a point (SP3), worth the same as a sweep.</summary>
    public double SameSuit { get; init; } = 10.0;

    /// <summary>
    /// Cost of dropping an ace, which happens only on an empty table (with cards there it always captures, P8):
    /// kept, it can take the whole table later. Owner's remark (1.7.2): "non ha senso buttarlo se il tavolo è vuoto".
    /// Measured: L1 with 4 beats L1 without in 53.8% of 10,000 matches; it still drops an ace in 1.2% of the cases
    /// (e.g. when every other card is worth much more).
    /// </summary>
    public double AceDrop { get; init; } = 4.0;
}

/// <summary>
/// L1: always declares; otherwise scores every legal play one ply deep — value captured, sweeps,
/// value given away by a drop, risk that the next opponent sweeps what we leave (estimated from
/// the cards still unseen). Ties are broken randomly.
/// </summary>
public sealed class GreedyBot(GreedyWeights? weights = null) : IBot
{
    private const double Epsilon = 1e-9;
    private readonly GreedyWeights _w = weights ?? GreedyWeights.Default;

    public string Name => "L1-Greedy";

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
            return declare; // A3: declaring is free, and points are lost otherwise
        }

        var plays = legal.Cast<PlayCard>().ToList();
        var scores = plays.Select(p => Evaluate(p, view, memory)).ToList();
        double best = scores.Max();
        var bestPlays = plays.Where((_, i) => scores[i] >= best - Epsilon).ToList();
        return bestPlays[rng.NextInt(bestPlays.Count)];
    }

    /// <summary>Heuristic value of a play for our team. Public for tests and tuning.</summary>
    public double Evaluate(PlayCard play, PlayerView view, BotMemory memory)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(memory);

        bool lastPlay = view.PlaysInRound == view.Variant.PlaysPerRound() - 1;
        double score;
        ImmutableArray<Card> tableAfter;

        if (play.Capture is { } capture)
        {
            tableAfter = view.Table.RemoveRange(capture.Cards);
            score = CardValue(play.Card) + capture.Cards.Sum(CardValue);
            if (tableAfter.IsEmpty && !lastPlay)
            {
                score += _w.Sweep;
            }

            if (view.Variant.HasSameSuitBonus() && capture.Cards.All(c => c.Suit == play.Card.Suit))
            {
                score += _w.SameSuit;
            }
        }
        else
        {
            tableAfter = view.Table.Add(play.Card);
            score = -_w.DropCost * CardValue(play.Card);
            if (play.Card.Rank == Rank.Ace)
            {
                score -= _w.AceDrop;
            }
        }

        if (!lastPlay)
        {
            score -= _w.SweepRisk * OpponentSweepProbability(tableAfter, view, memory);
        }

        return score;
    }

    public double CardValue(Card card)
    {
        double value = _w.Card;
        if (card.IsCoins)
        {
            value += _w.Coin;
            if (card.Rank <= Rank.Three)
            {
                value += _w.SpincioStart;
            }
        }

        if (card.Rank == Rank.Seven)
        {
            value += _w.Seven;
        }

        if (card == RoundScoring.Settebello)
        {
            value += _w.Settebello;
        }
        else if (card == RoundScoring.Rebello)
        {
            value += _w.Rebello;
        }

        return value;
    }

    /// <summary>
    /// Probability that the next seat (always an opponent) holds a card that sweeps <paramref name="table"/>,
    /// assuming its cards are a uniform sample of the cards we have not seen (hypergeometric).
    /// </summary>
    public static double OpponentSweepProbability(IReadOnlyList<Card> table, PlayerView view, BotMemory memory)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(memory);

        if (SweepValue(table) is not { } value)
        {
            return 0;
        }

        int opponentCards = view.HandCounts[view.Seat.Next().Index];
        int pool = memory.UnseenCount;
        int matching = memory.UnseenOfValue(value);
        if (opponentCards == 0 || matching <= 0 || pool <= 0)
        {
            return 0;
        }

        double noMatch = 1.0;
        for (int i = 0; i < opponentCards && i < pool; i++)
        {
            noMatch *= Math.Max(0, pool - matching - i) / (double)(pool - i);
        }

        return 1.0 - noMatch;
    }

    /// <summary>The card value that would capture the whole table, if any (P1).</summary>
    public static int? SweepValue(IReadOnlyList<Card> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count == 0)
        {
            return null;
        }

        // One card: taken by equality. Several: by their sum — no single table card can equal the sum
        // of two or more positive values, so P2 never blocks it.
        int sum = table.Sum(c => c.Value);
        return sum <= (int)Rank.King ? sum : null;
    }
}
