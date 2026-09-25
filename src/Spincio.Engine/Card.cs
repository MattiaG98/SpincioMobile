namespace Spincio.Engine;

/// <summary>Suits of the Italian 40-card deck (Piacentine).</summary>
public enum Suit
{
    Coins,
    Cups,
    Swords,
    Clubs,
}

/// <summary>Ranks; the numeric value is the capture value (S1).</summary>
public enum Rank
{
    Ace = 1,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Jack,
    Knight,
    King,
}

public readonly record struct Card(Rank Rank, Suit Suit) : IComparable<Card>
{
    public int Value => (int)Rank;

    public bool IsCoins => Suit == Suit.Coins;

    /// <summary>All 40 cards in canonical order (suit, then rank).</summary>
    public static IReadOnlyList<Card> FullDeck { get; } =
        [.. Enum.GetValues<Suit>().SelectMany(s => Enum.GetValues<Rank>().Select(r => new Card(r, s)))];

    public int CompareTo(Card other) =>
        Suit != other.Suit ? Suit.CompareTo(other.Suit) : Rank.CompareTo(other.Rank);

    public static bool operator <(Card left, Card right) => left.CompareTo(right) < 0;
    public static bool operator >(Card left, Card right) => left.CompareTo(right) > 0;
    public static bool operator <=(Card left, Card right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Card left, Card right) => left.CompareTo(right) >= 0;

    /// <summary>Test notation from SPEC §0, e.g. "7D", "KC".</summary>
    public override string ToString() => $"{RankSymbol(Rank)}{SuitSymbol(Suit)}";

    internal static char RankSymbol(Rank rank) => rank switch
    {
        Rank.Ace => 'A',
        Rank.Jack => 'J',
        Rank.Knight => 'N',
        Rank.King => 'K',
        _ => (char)('0' + (int)rank),
    };

    internal static char SuitSymbol(Suit suit) => suit switch
    {
        Suit.Coins => 'D',
        Suit.Cups => 'C',
        Suit.Swords => 'S',
        _ => 'B',
    };
}
