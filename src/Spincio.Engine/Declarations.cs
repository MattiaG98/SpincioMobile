using System.Collections.Immutable;

namespace Spincio.Engine;

[Flags]
public enum DeclarationKind
{
    None = 0,
    LowSum = 1,
    LowSumWithPair = 2,
    ThreeOfAKind = 4,

    /// <summary>Spincione, 4 cards: three of them equal and a low sum (SP5).</summary>
    LowSumWithThree = 8,

    /// <summary>Spincione, 4 cards: all equal (SP5).</summary>
    FourOfAKind = 16,

    /// <summary>Spincione: every card of the hand of one suit (SP6).</summary>
    Colore = 32,

    /// <summary>Spincione: the hand is a run of consecutive ranks, going round from the king to the ace (SP6).</summary>
    Scala = 64,
}

public readonly record struct DeclarationValue(DeclarationKind Kind, int Points)
{
    public static DeclarationValue None => default;
}

/// <summary>A declaration made at the table. Public: type and points only, never the cards (A4).</summary>
public sealed record DeclarationRecord(Seat Seat, int DealNumber, DeclarationKind Kind, int Points);

/// <summary>Hand points A1, and the Spincione's (SP4–SP7).</summary>
public static class Declarations
{
    public const int LowSumMax = 9;
    public const int LowSumPoints = 2;
    public const int LowSumWithPairPoints = 3;
    public const int ThreeOfAKindPoints = 7;

    /// <summary>SP5: with 4 cards the low sum goes up to 10.</summary>
    public const int FourCardLowSumMax = 10;

    /// <summary>SP6: colore and scala, 2 points each before the first-hand doubling.</summary>
    public const int ColorePoints = 2;
    public const int ScalaPoints = 2;

    /// <summary>The value of <paramref name="hand"/> at deal <paramref name="dealNumber"/> of <paramref name="variant"/>.</summary>
    public static DeclarationValue Evaluate(IReadOnlyCollection<Card> hand, GameVariant variant, int dealNumber) =>
        variant == GameVariant.Spincione ? EvaluateSpincione(hand, firstHand: dealNumber == 1) : Evaluate(hand);

    public static DeclarationValue Evaluate(IReadOnlyCollection<Card> hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        if (hand.Count == 0)
        {
            return DeclarationValue.None;
        }

        var rankCounts = hand.GroupBy(c => c.Rank).Select(g => g.Count()).ToImmutableArray();
        bool threeOfAKind = rankCounts.Any(n => n == 3);
        bool pair = rankCounts.Any(n => n == 2);
        bool lowSum = hand.Sum(c => c.Value) <= LowSumMax;

        var kind = DeclarationKind.None;
        int points = 0;
        if (threeOfAKind)
        {
            kind |= DeclarationKind.ThreeOfAKind;
            points += ThreeOfAKindPoints;
        }

        if (lowSum)
        {
            kind |= pair ? DeclarationKind.LowSumWithPair : DeclarationKind.LowSum;
            points += pair ? LowSumWithPairPoints : LowSumPoints;
        }

        return new DeclarationValue(kind, points);
    }

    /// <summary>
    /// SP4–SP7. The Spincio's hand points generalised to the hand size (all cards equal = 7 plus the low sum, all but
    /// one equal with a low sum = 3, a low sum = 2), plus colore and scala; doubled in the first hand.
    /// </summary>
    private static DeclarationValue EvaluateSpincione(IReadOnlyCollection<Card> hand, bool firstHand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        int count = hand.Count;
        if (count == 0)
        {
            return DeclarationValue.None;
        }

        int mostOfARank = hand.GroupBy(c => c.Rank).Max(g => g.Count());
        bool allEqual = count >= 3 && mostOfARank == count;
        bool lowSum = hand.Sum(c => c.Value) <= (count >= 4 ? FourCardLowSumMax : LowSumMax);

        var kind = DeclarationKind.None;
        int points = 0;
        if (allEqual)
        {
            kind |= count >= 4 ? DeclarationKind.FourOfAKind : DeclarationKind.ThreeOfAKind;
            points += ThreeOfAKindPoints;
        }

        if (lowSum)
        {
            if (!allEqual && mostOfARank == count - 1 && count >= 3)
            {
                kind |= count >= 4 ? DeclarationKind.LowSumWithThree : DeclarationKind.LowSumWithPair;
                points += LowSumWithPairPoints;
            }
            else
            {
                kind |= DeclarationKind.LowSum;
                points += LowSumPoints;
            }
        }

        if (count >= 3 && hand.All(c => c.Suit == hand.First().Suit))
        {
            kind |= DeclarationKind.Colore;
            points += ColorePoints;
        }

        if (IsScala(hand))
        {
            kind |= DeclarationKind.Scala;
            points += ScalaPoints;
        }

        return new DeclarationValue(kind, firstHand ? points * 2 : points);
    }

    /// <summary>SP6: different ranks, consecutive, going round after the king (J-N-K-A, K-A-2 are runs).</summary>
    private static bool IsScala(IReadOnlyCollection<Card> hand)
    {
        int ranks = Enum.GetValues<Rank>().Length;
        var values = hand.Select(c => c.Value).ToHashSet();
        if (hand.Count < 3 || values.Count != hand.Count)
        {
            return false;
        }

        foreach (int start in values)
        {
            bool run = true;
            for (int i = 1; i < hand.Count && run; i++)
            {
                run = values.Contains(((start - 1 + i) % ranks) + 1);
            }

            if (run)
            {
                return true;
            }
        }

        return false;
    }
}
