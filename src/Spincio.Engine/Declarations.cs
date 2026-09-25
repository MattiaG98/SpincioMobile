using System.Collections.Immutable;

namespace Spincio.Engine;

[Flags]
public enum DeclarationKind
{
    None = 0,
    LowSum = 1,
    LowSumWithPair = 2,
    ThreeOfAKind = 4,
}

public readonly record struct DeclarationValue(DeclarationKind Kind, int Points)
{
    public static DeclarationValue None => default;
}

/// <summary>A declaration made at the table. Public: type and points only, never the cards (A4).</summary>
public sealed record DeclarationRecord(Seat Seat, int DealNumber, DeclarationKind Kind, int Points);

/// <summary>Hand points A1.</summary>
public static class Declarations
{
    public const int LowSumMax = 9;
    public const int LowSumPoints = 2;
    public const int LowSumWithPairPoints = 3;
    public const int ThreeOfAKindPoints = 7;

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
}
