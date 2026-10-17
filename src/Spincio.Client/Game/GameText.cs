using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>Italian labels for the UI. Seats are named relative to the viewer, who sits at the bottom.</summary>
public static class GameText
{
    /// <summary>Position of <paramref name="seat"/> as seen from <paramref name="me"/>: 0 = me, 1 = right, 2 = partner, 3 = left.</summary>
    public static int Relative(Seat seat, Seat me) => (seat.Index - me.Index + Seat.Count) % Seat.Count;

    public static Seat AtPosition(Seat me, int position) => new((me.Index + position) % Seat.Count);

    /// <summary>Names of the CPU players in the offline game, chosen by the owner: partner, right opponent, left opponent.</summary>
    public const string PartnerName = "Titti";
    public const string RightOpponentName = "Tito";
    public const string LeftOpponentName = "Vava";

    /// <summary>Default names for the offline game.</summary>
    public static string SeatName(Seat seat, Seat me) => Relative(seat, me) switch
    {
        0 => "Tu",
        1 => RightOpponentName,
        2 => PartnerName,
        _ => LeftOpponentName,
    };

    /// <summary>
    /// Avatar of each offline CPU (original drawings in wwwroot/avatars), by position like <see cref="SeatName"/>:
    /// Tito on the right, Titti the partner, Vava on the left. None for the viewer.
    /// </summary>
    public static string? Avatar(Seat seat, Seat me) => Relative(seat, me) switch
    {
        1 => "avatars/tito.svg",
        2 => "avatars/titti.svg",
        3 => "avatars/vava.svg",
        _ => null,
    };

    /// <summary>A seat's declarations in the deal being played: they tell something about the hand in play (A4).</summary>
    public static IReadOnlyList<DeclarationRecord> DeclarationsThisDeal(PlayerView view, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Declarations.Where(d => d.Seat == seat && d.DealNumber == view.DealNumber)];
    }

    public static string TeamName(Team team, Seat me) => team == me.Team ? "Noi" : "Loro";

    public static Team Other(Team team) => team == Team.A ? Team.B : Team.A;

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
        _ => CombinedDeclarationName(kind),
    };

    /// <summary>The Spincione's declarations add up (SP4–SP6): "Colore + Scala", "Quattro uguali + Mano fino a 10"…</summary>
    private static string CombinedDeclarationName(DeclarationKind kind)
    {
        // A first-hand declaration (4 cards) has its low sum up to 10, and its points are doubled.
        bool fourCards = kind.HasFlag(DeclarationKind.DoubledFirstHand);
        var parts = new List<string>();
        if (kind.HasFlag(DeclarationKind.FourOfAKind))
        {
            parts.Add("Quattro uguali");
        }

        if (kind.HasFlag(DeclarationKind.ThreeOfAKind))
        {
            parts.Add("Tris");
        }

        if (kind.HasFlag(DeclarationKind.LowSumWithThree))
        {
            parts.Add("Mano fino a 10 con tre uguali");
        }

        if (kind.HasFlag(DeclarationKind.LowSumWithPair))
        {
            parts.Add("Mano fino a 9 con coppia");
        }

        if (kind.HasFlag(DeclarationKind.LowSum))
        {
            parts.Add(fourCards ? "Mano fino a 10" : "Mano fino a 9");
        }

        if (kind.HasFlag(DeclarationKind.Colore))
        {
            parts.Add("Colore");
        }

        if (kind.HasFlag(DeclarationKind.Scala))
        {
            parts.Add("Scala");
        }

        string name = parts.Count > 0 ? string.Join(" + ", parts) : kind.ToString();
        return fourCards ? name + " · prima mano ×2" : name;
    }

    /// <summary>The name of a game mode (SPEC §1.7).</summary>
    public static string ModeName(GameVariant variant) => variant == GameVariant.Spincione ? "Spincione" : "Spincio";

    /// <summary>
    /// Cards with a key unique among them, for rendering lists: in the Spincione the same card can appear twice (SP1),
    /// and the second one is keyed "7D#2".
    /// </summary>
    public static IReadOnlyList<(Card Card, string Key)> Keyed(IEnumerable<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var seen = new Dictionary<Card, int>();
        var keyed = new List<(Card, string)>();
        foreach (var card in cards)
        {
            int n = seen[card] = seen.GetValueOrDefault(card) + 1;
            keyed.Add((card, n == 1 ? card.ToString() : $"{card}#{n}"));
        }

        return keyed;
    }

    public static string WinReasonText(WinReason reason) => reason switch
    {
        WinReason.AllCoins => "tutti i 10 denari",
        _ => "punteggio",
    };

    /// <summary>
    /// The banner a play deserves: "MARIANA" for a jack taking a 5 and a 3 (instead of "Spazzino!" when it also empties
    /// the table: the sweep still scores), "Spazzino!" for any other sweep, null otherwise.
    /// </summary>
    public static string? Banner(CardPlayed play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return play.IsMariana() ? "MARIANA" : play.IsSweep ? "Spazzino!" : play.IsSameSuit ? "Stesso seme!" : null;
    }

    private static string Callout(CardPlayed play)
    {
        string callout = (play.IsMariana(), play.IsSweep) switch
        {
            (true, true) => " — MARIANA! +1",
            (true, false) => " — MARIANA!",
            (false, true) => " — SPAZZINO!",
            _ => "",
        };
        return play.IsSameSuit ? callout + " — STESSO SEME! +1" : callout; // SP3, Spincione
    }

    /// <summary>One feed line for an event the viewer may see, or null for events not worth a line.</summary>
    public static string? Describe(GameEvent gameEvent, Seat me, Func<Seat, string> seatName) => gameEvent switch
    {
        RoundStarted r when r.MatchNumber > 0 => $"Spareggio {r.MatchNumber}, smazzata {r.RoundNumber}: mescola {seatName(r.Dealer)}",
        RoundStarted r => $"Smazzata {r.RoundNumber}: mescola {seatName(r.Dealer)}",
        DeckReshuffled => "Almeno due assi in tavola: si rimescola",
        Declared d => $"{Subject(d.Seat, me, seatName, "accusi", "accusa")}: {DeclarationName(d.Kind)} (+{d.Points})",
        CardPlayed { Captured.IsEmpty: true } p => $"{Subject(p.Seat, me, seatName, "cali", "cala")} {CardName(p.Card)}",
        CardPlayed p => $"{Subject(p.Seat, me, seatName, "prendi", "prende")} {Cards(p.Captured)} con {CardName(p.Card)}" + Callout(p),
        TableAwarded { Team: null } a => $"Carte rimaste in tavola a nessuno: {Cards(a.Cards)}",
        TableAwarded a => $"Carte rimaste in tavola a {TeamName(a.Team!.Value, me)}: {Cards(a.Cards)}",
        TiebreakStarted t => $"Pareggio! Si gioca lo spareggio {t.MatchNumber}",
        MatchEnded m => $"Partita finita: vince {TeamName(m.Winner, me)} ({WinReasonText(m.Reason)})",
        _ => null,
    };

    /// <summary>"Tu prendi" for the viewer, "Titti prende" for everybody else.</summary>
    private static string Subject(Seat seat, Seat me, Func<Seat, string> seatName, string secondPerson, string thirdPerson) =>
        seat == me ? $"{seatName(seat)} {secondPerson}" : $"{seatName(seat)} {thirdPerson}";
}
