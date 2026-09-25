using System.Collections.Immutable;

namespace Spincio.Engine;

/// <summary>
/// What one seat is allowed to know (AT-31): own hand, table, score, public declarations, hand sizes.
/// Never any captured pile.
/// </summary>
public sealed record PlayerView(
    Seat Seat,
    MatchPhase Phase,
    int MatchNumber,
    int RoundNumber,
    Seat Dealer,
    Seat ToPlay,
    int DealNumber,
    int PlaysInRound,
    ImmutableArray<Card> Hand,
    ImmutableArray<Card> Table,
    int DeckCount,
    ImmutableArray<int> HandCounts,
    TeamScores Score,
    ImmutableArray<DeclarationRecord> Declarations,
    DeclarationValue AvailableDeclaration,
    Team? Winner)
{
    public bool IsMyTurn => Phase == MatchPhase.AwaitingPlay && ToPlay == Seat;

    public bool CanDeclare => AvailableDeclaration.Points > 0;
}
