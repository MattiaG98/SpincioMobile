using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

/// <summary>P8 (SPEC v1.4): the ace takes the whole table ("asso pigliatutto"), unless an ace is already on it.</summary>
public class AceTests
{
    private static string[] OptionsOf(string played, string table) =>
        [.. Captures.Options(C(played), Many(table)).Select(o => o.ToString())];

    [Fact]
    public void AT_33_ace_takes_the_whole_table()
    {
        OptionsOf("AD", "3S,KB,7C").ShouldBe([Take("3S,KB,7C").ToString()]);
    }

    [Fact]
    public void AT_34_ace_on_a_table_with_an_ace_takes_only_that_ace()
    {
        OptionsOf("AD", "AC,5S,JB").ShouldBe(["{AC}"]);
    }

    [Fact]
    public void AT_39_ace_taking_the_only_ace_on_the_table_is_a_sweep()
    {
        var state = new Scenario { PlaysInRound = 5 }.Hand(0, "AD").Table("AC").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("AD"), Take("AC")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeTrue();
        step.State.Score.ShouldBe(new TeamScores(1, 0));
    }

    [Fact]
    public void AT_35_ace_capture_is_a_sweep_worth_one_point()
    {
        var state = new Scenario { PlaysInRound = 5 }.Hand(0, "AD").Table("3S,KB,7C").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("AD"), Take("3S,KB,7C")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeTrue();
        step.State.Table.ShouldBeEmpty();
        step.State.Score.ShouldBe(new TeamScores(1, 0));
    }

    [Fact]
    public void AT_36_ace_on_empty_table_is_just_dropped()
    {
        var state = new Scenario { PlaysInRound = 5 }.Hand(0, "AD").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("AD")));

        var played = step.EventsOf<CardPlayed>().ShouldHaveSingleItem();
        played.Captured.ShouldBeEmpty();
        played.IsSweep.ShouldBeFalse();
        step.State.Table.ShouldBe([C("AD")]);
        step.State.Score.ShouldBe(TeamScores.Zero);
    }

    [Fact]
    public void AT_37_ace_on_the_last_play_takes_the_table_without_sweep()
    {
        var state = new Scenario().LastPlay(Scenario.LeftoverTarget.PileB).Hand(0, "AD").Table("3S,4B").Build();

        var step = state.Ok(new PlayCard(new Seat(0), C("AD"), Take("3S,4B")));

        step.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsSweep.ShouldBeFalse();
        step.EventsOf<RoundScored>().ShouldHaveSingleItem().Ledger.Sweeps.ShouldBe(TeamScores.Zero);
    }

    [Fact]
    public void AT_38_ace_must_take_the_whole_table_or_the_ace_on_it()
    {
        var seat = new Seat(0);
        var noAce = new Scenario { PlaysInRound = 5 }.Hand(0, "AD").Table("3S,5B").Build();
        SpincioEngine.Apply(noAce, new PlayCard(seat, C("AD"))).IsSuccess.ShouldBeFalse();
        SpincioEngine.Apply(noAce, new PlayCard(seat, C("AD"), Take("3S"))).IsSuccess.ShouldBeFalse();

        var withAce = new Scenario { PlaysInRound = 5 }.Hand(0, "AD").Table("AC,5S").Build();
        SpincioEngine.Apply(withAce, new PlayCard(seat, C("AD"))).IsSuccess.ShouldBeFalse();
        SpincioEngine.Apply(withAce, new PlayCard(seat, C("AD"), Take("AC,5S"))).IsSuccess.ShouldBeFalse();
    }
}
