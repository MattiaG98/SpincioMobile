using System.Collections.Immutable;

namespace Spincio.Engine;

public enum MatchPhase
{
    AwaitingPlay,
    MatchOver,
}

/// <summary>Points scored live during the current round (sweeps P6, declarations A1, same-suit captures SP3).</summary>
public sealed record RoundLedger(TeamScores Sweeps, TeamScores Declarations)
{
    public static RoundLedger Empty { get; } = new(TeamScores.Zero, TeamScores.Zero);

    /// <summary>SP3, Spincione only: captures all of one suit.</summary>
    public TeamScores SameSuit { get; init; }
}

/// <summary>
/// Full authoritative state. Immutable; contains hidden information, so it never leaves the engine/server
/// except through <see cref="SpincioEngine.ViewFor"/>.
/// </summary>
public sealed record MatchState
{
    /// <summary>Spincio; the Spincione has more (<see cref="VariantRules.PlaysPerRound"/>).</summary>
    public const int PlaysPerRound = 36;

    /// <summary>Cards a hand from the second deal on (the Spincione's first deal has 4, SP2).</summary>
    public const int CardsPerHand = 3;

    public const int InitialTableCards = 4;

    /// <summary>The game mode (SPEC §1.7). Part of the match from its start.</summary>
    public GameVariant Variant { get; init; }

    public required Pcg32 Rng { get; init; }

    public required MatchPhase Phase { get; init; }

    /// <summary>0 = main match, 1.. = tiebreak matches (E2).</summary>
    public required int MatchNumber { get; init; }

    /// <summary>1-based round number within the current (tiebreak) match.</summary>
    public required int RoundNumber { get; init; }

    public required Seat Dealer { get; init; }

    public required Seat ToPlay { get; init; }

    /// <summary>1..3 within the round (S4); 1..6 in the Spincione (SP2).</summary>
    public required int DealNumber { get; init; }

    /// <summary>Plays made in the current round, 0..36 (0..76 in the Spincione).</summary>
    public required int PlaysInRound { get; init; }

    /// <summary>Indexed by seat.</summary>
    public required ImmutableArray<ImmutableArray<Card>> Hands { get; init; }

    public required ImmutableArray<Card> Table { get; init; }

    public required ImmutableArray<Card> Deck { get; init; }

    /// <summary>Captured cards, indexed by team. Hidden from everybody (E3).</summary>
    public required ImmutableArray<ImmutableArray<Card>> Piles { get; init; }

    /// <summary>Table cards nobody took (P7, C7).</summary>
    public required ImmutableArray<Card> Unclaimed { get; init; }

    public Team? LastCapturingTeam { get; init; }

    public required TeamScores Score { get; init; }

    public required RoundLedger Ledger { get; init; }

    /// <summary>Indexed by seat: has the seat played a card in the current deal?</summary>
    public required ImmutableArray<bool> PlayedThisDeal { get; init; }

    /// <summary>Declarations made in the current round.</summary>
    public required ImmutableArray<DeclarationRecord> Declarations { get; init; }

    public Team? Winner { get; init; }

    /// <summary>Number of accepted commands since the match started (future <c>expectedSeq</c>).</summary>
    public required int Sequence { get; init; }

    public ImmutableArray<Card> HandOf(Seat seat) => Hands[seat.Index];

    public ImmutableArray<Card> PileOf(Team team) => Piles[(int)team];

    public bool HasDeclaredThisDeal(Seat seat) =>
        Declarations.Any(d => d.Seat == seat && d.DealNumber == DealNumber);

    /// <summary>Every card the state holds, wherever it is. Always the whole deck of the variant (40 or 80 cards).</summary>
    public IEnumerable<Card> AllCards() =>
        Hands.SelectMany(h => h).Concat(Table).Concat(Deck).Concat(Piles.SelectMany(p => p)).Concat(Unclaimed);
}
