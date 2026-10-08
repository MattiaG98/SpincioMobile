using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

/// <summary>Mariana (SPEC v1.4, scritte): a jack taking a 5 and a 3. A name only: the points do not change.</summary>
public class CalloutTests
{
    private static CardPlayed Play(string card, string captured, bool sweep = false) =>
        new(new Seat(1), C(card), [.. Many(captured)], sweep);

    [Theory]
    [InlineData("JD", "5S,3C", true)]
    [InlineData("JB", "3D,5D", true)]
    [InlineData("JC", "6S,2C", false)] // another sum to 8
    [InlineData("JC", "JS", false)] // equal value
    [InlineData("AD", "5S,3C", false)] // the ace takes the whole table (P8): not a jack
    [InlineData("KD", "5S,3C,2B", false)] // a 5 and a 3, but not with a jack and not only them
    [InlineData("JD", "", false)] // a drop
    public void AT_40_mariana_is_a_jack_taking_a_five_and_a_three(string card, string captured, bool mariana)
    {
        Play(card, captured).IsMariana().ShouldBe(mariana);
    }

    [Fact]
    public void AT_40_mariana_scores_nothing_and_a_mariana_sweep_is_still_one_point()
    {
        var withCardsLeft = new Scenario { PlaysInRound = 5 }.Hand(0, "JD").Table("5S,3C,KB").Build()
            .Ok(new PlayCard(new Seat(0), C("JD"), Take("5S,3C")));
        var sweep = new Scenario { PlaysInRound = 5 }.Hand(0, "JD").Table("5S,3C").Build()
            .Ok(new PlayCard(new Seat(0), C("JD"), Take("5S,3C")));

        withCardsLeft.EventsOf<CardPlayed>().ShouldHaveSingleItem().IsMariana().ShouldBeTrue();
        withCardsLeft.State.Score.ShouldBe(TeamScores.Zero);
        var swept = sweep.EventsOf<CardPlayed>().ShouldHaveSingleItem();
        swept.IsMariana().ShouldBeTrue();
        swept.IsSweep.ShouldBeTrue();
        sweep.State.Score.ShouldBe(new TeamScores(1, 0));
    }
}
