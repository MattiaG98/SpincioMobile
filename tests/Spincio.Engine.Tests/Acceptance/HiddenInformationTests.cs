using System.Collections.Immutable;
using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class HiddenInformationTests
{
    [Fact]
    public void AT_31_view_has_own_hand_table_score_declarations_and_hand_counts()
    {
        var state = new Scenario { Score = new TeamScores(4, 7) }
            .Hand(0, "2C,2S,5B")
            .Hand(1, "JC,NS")
            .Hand(2, "KB")
            .Table("3D,4D")
            .Build();
        state = state.Ok(new Declare(new Seat(0))).State;

        var view = SpincioEngine.ViewFor(state, new Seat(1));

        view.Hand.ShouldBe(Many("JC,NS"));
        view.Table.ShouldBe(Many("3D,4D"));
        view.Score.ShouldBe(new TeamScores(7, 7));
        view.Declarations.ShouldHaveSingleItem().Points.ShouldBe(3);
        view.HandCounts.ShouldBe([3, 2, 1, 0]);
    }

    [Fact]
    public void AT_31_view_never_exposes_captured_piles()
    {
        var cardCollections = typeof(PlayerView).GetProperties()
            .Where(p => p.PropertyType == typeof(ImmutableArray<Card>) || p.PropertyType == typeof(ImmutableArray<ImmutableArray<Card>>))
            .Select(p => p.Name);
        cardCollections.ShouldBe(["Hand", "Table"], ignoreOrder: true);

        Playout.RandomMatch(seed: 31, onStep: step =>
        {
            var piles = step.State.Piles.SelectMany(p => p).ToHashSet();
            foreach (var seat in Seat.All)
            {
                var view = SpincioEngine.ViewFor(step.State, seat);
                view.Hand.Concat(view.Table).ShouldNotContain(c => piles.Contains(c));
            }
        });
    }

    [Fact]
    public void Private_hand_events_reach_only_their_seat()
    {
        var start = SpincioEngine.NewMatch(seed: 5);

        foreach (var seat in Seat.All)
        {
            var dealt = start.EventsFor(seat).OfType<HandDealt>().ShouldHaveSingleItem();
            dealt.Seat.ShouldBe(seat);
            dealt.Cards.ShouldBe(start.State.HandOf(seat));
        }
    }

    [Fact]
    public void AT_32_nobody_captured_leftover_goes_to_nobody()
    {
        var state = new Scenario { LastCapturingTeam = null }
            .LastPlay(Scenario.LeftoverTarget.PileA)
            .Hand(0, "KC")
            .Table("2C,6S")
            .Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("KC")));

        var awarded = step.EventsOf<TableAwarded>().ShouldHaveSingleItem();
        awarded.Team.ShouldBeNull();
        awarded.Cards.ShouldBe(Many("2C,6S,KC"), ignoreOrder: true);
        var scored = step.EventsOf<RoundScored>().ShouldHaveSingleItem();
        scored.Score.A.CardCount.ShouldBe(37);
        scored.Score.B.CardCount.ShouldBe(0);
    }
}
