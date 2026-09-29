using System.Collections.Immutable;

namespace Spincio.Engine;

/// <summary>Who may see an event. Hidden information is filtered here, in the engine (CLAUDE.md rule 4).</summary>
public readonly record struct Audience(Seat? Only)
{
    public static Audience All => default;

    public static Audience To(Seat seat) => new(seat);

    public bool Includes(Seat seat) => Only is null || Only == seat;
}

public abstract record GameEvent
{
    public virtual Audience Audience => Audience.All;
}

public sealed record RoundStarted(int MatchNumber, int RoundNumber, Seat Dealer) : GameEvent;

/// <summary>S5: the initial table had two or more aces; everything is reshuffled by the same dealer.</summary>
public sealed record DeckReshuffled(ImmutableArray<Card> RejectedTable) : GameEvent;

/// <summary>Private: only the receiving seat sees its cards.</summary>
public sealed record HandDealt(Seat Seat, ImmutableArray<Card> Cards) : GameEvent
{
    public override Audience Audience => Audience.To(Seat);
}

/// <summary>A deal is complete. <paramref name="Table"/> is the face-up table (dealt only on deal 1).</summary>
public sealed record DealStarted(int DealNumber, ImmutableArray<Card> Table, int DeckCount) : GameEvent;

/// <summary>A4: type and points only, never the cards.</summary>
public sealed record Declared(Seat Seat, DeclarationKind Kind, int Points) : GameEvent;

/// <summary>A card was played. Empty <paramref name="Captured"/> means a drop.</summary>
public sealed record CardPlayed(Seat Seat, Card Card, ImmutableArray<Card> Captured, bool IsSweep) : GameEvent;

/// <summary>P7: cards left on the table at the end of the round; <paramref name="Team"/> null = nobody.</summary>
public sealed record TableAwarded(Team? Team, ImmutableArray<Card> Cards) : GameEvent;

/// <summary>E3: detailed end-of-round summary. <paramref name="Ledger"/> recaps points already scored live.</summary>
public sealed record RoundScored(RoundScore Score, RoundLedger Ledger, TeamScores MatchScore) : GameEvent;

public sealed record TiebreakStarted(int MatchNumber) : GameEvent;

public sealed record MatchEnded(Team Winner, WinReason Reason, TeamScores FinalScore) : GameEvent;

public sealed record Transition(MatchState State, ImmutableArray<GameEvent> Events)
{
    public IEnumerable<GameEvent> EventsFor(Seat seat) => Events.Where(e => e.Audience.Includes(seat));
}
