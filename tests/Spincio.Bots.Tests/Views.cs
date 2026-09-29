using System.Collections.Immutable;
using Spincio.Engine;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Bots.Tests;

/// <summary>Hand-built views: bots must work from a <see cref="PlayerView"/> alone.</summary>
internal static class Views
{
    public static PlayerView For(
        string hand,
        string table,
        int seat = 0,
        int playsInRound = 4,
        ImmutableArray<int>? handCounts = null,
        DeclarationValue? declarable = null)
    {
        var cards = Many(hand);
        var me = new Seat(seat);
        return new PlayerView(
            Seat: me,
            Phase: MatchPhase.AwaitingPlay,
            MatchNumber: 0,
            RoundNumber: 1,
            Dealer: new Seat((seat + 3) % 4),
            ToPlay: me,
            DealNumber: 1,
            PlaysInRound: playsInRound,
            Hand: cards,
            Table: Many(table),
            DeckCount: 24,
            HandCounts: handCounts ?? [3, 3, 3, 3],
            Score: TeamScores.Zero,
            Declarations: [],
            AvailableDeclaration: declarable ?? DeclarationValue.None,
            Winner: null);
    }

    /// <summary>A memory that has seen exactly the given cards.</summary>
    public static BotMemory Memory(string seen, int seat = 0)
    {
        var memory = new BotMemory(new Seat(seat));
        memory.Observe(new HandDealt(new Seat(seat), Many(seen)));
        return memory;
    }
}
