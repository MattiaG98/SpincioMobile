using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>Italian labels for the UI. Seats counter-clockwise from the human at the bottom.</summary>
public static class GameText
{
    public static string SeatName(Seat seat) => seat.Index switch
    {
        0 => "Tu",
        1 => "Est",
        2 => "Compagno",
        _ => "Ovest",
    };

    public static string TeamName(Team team) => team == Team.A ? "Noi" : "Loro";

    public static string RankLabel(Rank rank) => rank switch
    {
        Rank.Ace => "A",
        Rank.Jack => "F",
        Rank.Knight => "C",
        Rank.King => "R",
        _ => ((int)rank).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string SuitName(Suit suit) => suit switch
    {
        Suit.Coins => "Denari",
        Suit.Cups => "Coppe",
        Suit.Swords => "Spade",
        _ => "Bastoni",
    };

    public static string CardName(Card card) => $"{RankName(card.Rank)} di {SuitName(card.Suit).ToLowerInvariant()}";

    public static string RankName(Rank rank) => rank switch
    {
        Rank.Ace => "Asso",
        Rank.Jack => "Fante",
        Rank.Knight => "Cavallo",
        Rank.King => "Re",
        _ => ((int)rank).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Cards(IEnumerable<Card> cards) => string.Join(" + ", cards.Select(CardName));

    public static string DeclarationName(DeclarationKind kind) => kind switch
    {
        DeclarationKind.LowSum => "Mano fino a 9",
        DeclarationKind.LowSumWithPair => "Mano fino a 9 con coppia",
        DeclarationKind.ThreeOfAKind => "Tris",
        DeclarationKind.ThreeOfAKind | DeclarationKind.LowSum => "Tris fino a 9",
        _ => kind.ToString(),
    };

    public static string WinReasonText(WinReason reason) => reason switch
    {
        WinReason.AllCoins => "tutti i 10 denari",
        _ => "punteggio",
    };

    /// <summary>One feed line for an event the human may see, or null for events not worth a line.</summary>
    public static string? Describe(GameEvent gameEvent) => gameEvent switch
    {
        RoundStarted r when r.MatchNumber > 0 => $"Spareggio {r.MatchNumber}, smazzata {r.RoundNumber}: mescola {SeatName(r.Dealer)}",
        RoundStarted r => $"Smazzata {r.RoundNumber}: mescola {SeatName(r.Dealer)}",
        DeckReshuffled => "Almeno due assi in tavola: si rimescola",
        Declared d => $"{SeatName(d.Seat)} accusa: {DeclarationName(d.Kind)} (+{d.Points})",
        CardPlayed { Captured.IsEmpty: true } p => $"{SeatName(p.Seat)} cala {CardName(p.Card)}",
        CardPlayed p => $"{SeatName(p.Seat)} prende {Cards(p.Captured)} con {CardName(p.Card)}" + (p.IsSweep ? " — SPAZZINO!" : ""),
        TableAwarded { Team: null } a => $"Carte rimaste in tavola a nessuno: {Cards(a.Cards)}",
        TableAwarded a => $"Carte rimaste in tavola a {TeamName(a.Team!.Value)}: {Cards(a.Cards)}",
        TiebreakStarted t => $"Pareggio! Si gioca lo spareggio {t.MatchNumber}",
        MatchEnded m => $"Partita finita: vince {TeamName(m.Winner)} ({WinReasonText(m.Reason)})",
        _ => null,
    };
}
