namespace Spincio.Engine;

/// <summary>
/// Game modes (SPEC §1.7). The variant is part of the match: the same seed, commands and variant always give the
/// same match.
/// </summary>
public enum GameVariant
{
    /// <summary>One deck, 3 cards a hand (SPEC §1.1–1.5).</summary>
    Spincio,

    /// <summary>Two decks, a first hand of 4 cards, doubled first-hand declarations and its own extras (SPEC §1.7).</summary>
    Spincione,
}

/// <summary>What changes between the variants: the numbers of the deal and the switches of SPEC §1.7.</summary>
public static class VariantRules
{
    private static readonly IReadOnlyList<Card> TwoDecks = [.. Card.FullDeck, .. Card.FullDeck];

    /// <summary>How many copies of each card the deck holds (SP1).</summary>
    public static int DeckCopies(this GameVariant variant) => variant == GameVariant.Spincione ? 2 : 1;

    /// <summary>All the cards of the variant in canonical order, each repeated <see cref="DeckCopies"/> times.</summary>
    public static IReadOnlyList<Card> Deck(this GameVariant variant) =>
        variant == GameVariant.Spincione ? TwoDecks : Card.FullDeck;

    public static int CardCount(this GameVariant variant) => variant.Deck().Count;

    /// <summary>Cards dealt to each seat at deal <paramref name="dealNumber"/> (S4, SP2).</summary>
    public static int HandSize(this GameVariant variant, int dealNumber) =>
        variant == GameVariant.Spincione && dealNumber == 1 ? 4 : MatchState.CardsPerHand;

    /// <summary>Plays in a round: every card except the initial table is played once (36, or 76 for SP2).</summary>
    public static int PlaysPerRound(this GameVariant variant) => variant.CardCount() - MatchState.InitialTableCards;

    /// <summary>SP10: a card taking its twin scores this instead of the same-suit point; a sweep adds 1 (3 in all).</summary>
    public const int TwinPoints = 2;

    /// <summary>SP3 and SP10: a capture all of one suit is worth a point, a card taking its twin 2.</summary>
    public static bool HasSameSuitBonus(this GameVariant variant) => variant == GameVariant.Spincione;

    /// <summary>F7 applies to the Spincio only (SP9).</summary>
    public static bool AllCoinsWins(this GameVariant variant) => variant == GameVariant.Spincio;
}
