using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class SweepTests
{
    [Fact]
    public void AT_13_sweep_scores_one_point_immediately()
    {
        var state = new Scenario { PlaysInRound = 5 }.Hand(0, "7D").Table("3S,4B").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("7D"), Take("3S,4B")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeTrue();
        step.State.Score.ShouldBe(new TeamScores(1, 0));
        step.State.Ledger.Sweeps.ShouldBe(new TeamScores(1, 0));
    }

    [Fact]
    public void AT_14_sweep_on_last_play_does_not_count()
    {
        var state = new Scenario().LastPlay(Scenario.LeftoverTarget.PileB).Hand(0, "7D").Table("3S,4B").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("7D"), Take("3S,4B")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeFalse();
        var scored = step.EventsOf<RoundScored>().ShouldHaveSingleItem();
        scored.Ledger.Sweeps.ShouldBe(TeamScores.Zero);
        scored.Score.A.CardCount.ShouldBe(3); // the capture still happens
    }

    [Fact]
    public void AT_15_leftover_table_goes_to_last_capturing_team_without_sweep()
    {
        var state = new Scenario { ToPlay = new Seat(1), LastCapturingTeam = Team.A }
            .LastPlay(Scenario.LeftoverTarget.PileA)
            .Hand(1, "4C")
            .Table("2C,6S,4D")
            .Build();

        var step = state.Ok(new PlayCard(new Seat(1), C("4C"), Take("4D")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeFalse();
        var awarded = step.EventsOf<TableAwarded>().ShouldHaveSingleItem();
        awarded.Team.ShouldBe(Team.B);
        awarded.Cards.ShouldBe(Many("2C,6S"), ignoreOrder: true);
        var scored = step.EventsOf<RoundScored>().ShouldHaveSingleItem();
        scored.Score.B.CardCount.ShouldBe(4);
        scored.Ledger.Sweeps.ShouldBe(TeamScores.Zero);
    }

    [Fact]
    public void C6_drop_on_last_play_goes_to_last_capturing_team()
    {
        var state = new Scenario { LastCapturingTeam = Team.B }
            .LastPlay(Scenario.LeftoverTarget.PileA)
            .Hand(0, "KC")
            .Table("2C,6S")
            .Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("KC")));

        var awarded = step.EventsOf<TableAwarded>().ShouldHaveSingleItem();
        awarded.Team.ShouldBe(Team.B);
        awarded.Cards.ShouldBe(Many("2C,6S,KC"), ignoreOrder: true);
    }
}
