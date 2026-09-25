using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Engine.Tests.Support;

/// <summary>Parses the SPEC §0 notation: rank A,2..7,J,N,K + suit D,C,S,B (e.g. "7D", "KC").</summary>
internal static class Cards
{
    public static Card C(string notation)
    {
        if (notation.Length != 2)
        {
            throw new FormatException($"Bad card '{notation}'");
        }

        var rank = notation[0] switch
        {
            'A' => Rank.Ace,
            'J' => Rank.Jack,
            'N' => Rank.Knight,
            'K' => Rank.King,
            >= '2' and <= '7' => (Rank)(notation[0] - '0'),
            _ => throw new FormatException($"Bad rank in '{notation}'"),
        };
        var suit = notation[1] switch
        {
            'D' => Suit.Coins,
            'C' => Suit.Cups,
            'S' => Suit.Swords,
            'B' => Suit.Clubs,
            _ => throw new FormatException($"Bad suit in '{notation}'"),
        };
        return new Card(rank, suit);
    }

    /// <summary>"3S,4B" → [3S, 4B]. Empty string → [].</summary>
    public static ImmutableArray<Card> Many(string notation) =>
        string.IsNullOrWhiteSpace(notation)
            ? []
            : [.. notation.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(C)];

    public static CaptureOption Take(string notation) => new(Many(notation));

    public static ImmutableArray<Card> Coins => [.. Card.FullDeck.Where(c => c.IsCoins)];
}
