using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class DeclarationTests
{
    [Theory]
    [InlineData("AD,3C,5S", 2, DeclarationKind.LowSum)]
    [InlineData("2D,2C,5S", 3, DeclarationKind.LowSumWithPair)]
    [InlineData("2D,2C,6S", 0, DeclarationKind.None)]
    [InlineData("3D,3C,3S", 9, DeclarationKind.ThreeOfAKind | DeclarationKind.LowSum)]
    [InlineData("5D,5C,5S", 7, DeclarationKind.ThreeOfAKind)]
    [InlineData("KD,KC,KS", 7, DeclarationKind.ThreeOfAKind)]
    [InlineData("AD,AC,7S", 3, DeclarationKind.LowSumWithPair)]
    [InlineData("KD,2C,AS", 0, DeclarationKind.None)]
    public void AT_16_hand_points(string hand, int points, DeclarationKind kind)
    {
        Declarations.Evaluate(Many(hand)).ShouldBe(new DeclarationValue(kind, points));
    }

    [Fact]
    public void AT_17_zero_point_hand_cannot_declare()
    {
        var state = new Scenario().Hand(0, "KC,2S,AB").Build();

        SpincioEngine.Apply(state, new Declare(new Seat(0))).Error.ShouldBe(IllegalReason.NothingToDeclare);
        SpincioEngine.LegalCommands(state, new Seat(0)).OfType<Declare>().ShouldBeEmpty();
        SpincioEngine.ViewFor(state, new Seat(0)).CanDeclare.ShouldBeFalse();
    }

    [Fact]
    public void AT_18_declare_only_on_own_turn_before_first_play_of_the_deal()
    {
        var state = new Scenario { ToPlay = new Seat(1) }
            .Hand(0, "JD,ND,KD")
            .Hand(1, "JC,NS,KB")
            .Hand(2, "2C,2S,5B")
            .Hand(3, "JB,NC,KS")
            .Build();
        var seat2 = new Seat(2);

        SpincioEngine.Apply(state, new Declare(seat2)).Error.ShouldBe(IllegalReason.NotYourTurn);

        state = PlayFirstCard(state); // seat 1
        state.ToPlay.ShouldBe(seat2);
        SpincioEngine.LegalCommands(state, seat2).ShouldContain(new Declare(seat2));
        SpincioEngine.ViewFor(state, seat2).AvailableDeclaration.Points.ShouldBe(3);

        state = PlayFirstCard(state); // seat 2 plays without declaring
        state = PlayFirstCard(state); // seat 3
        state = PlayFirstCard(state); // seat 0
        state = PlayFirstCard(state); // seat 1
        state.ToPlay.ShouldBe(seat2);

        SpincioEngine.Apply(state, new Declare(seat2)).Error.ShouldBe(IllegalReason.AlreadyPlayedThisDeal);
        SpincioEngine.LegalCommands(state, seat2).OfType<Declare>().ShouldBeEmpty();
        state.Score.ShouldBe(TeamScores.Zero);
    }

    [Fact]
    public void AT_18_declare_adds_team_points_once()
    {
        var state = new Scenario().Hand(0, "2C,2S,5B").Build();

        var next = state.Ok(new Declare(new Seat(0))).State;

        next.Score.ShouldBe(new TeamScores(3, 0));
        SpincioEngine.Apply(next, new Declare(new Seat(0))).Error.ShouldBe(IllegalReason.AlreadyDeclared);
        next.ToPlay.ShouldBe(new Seat(0)); // declaring does not pass the turn
    }

    [Fact]
    public void AT_19_others_see_only_type_and_points()
    {
        var state = new Scenario().Hand(0, "2C,2S,5B").Hand(1, "JC,NS,KB").Build();

        var step = state.Ok(new Declare(new Seat(0)));

        var declared = step.EventsOf<Declared>().ShouldHaveSingleItem();
        declared.Audience.ShouldBe(Audience.All);
        typeof(Declared).GetProperties().Select(p => p.PropertyType)
            .ShouldNotContain(t => t == typeof(Card) || t.IsGenericType && t.GetGenericArguments().Contains(typeof(Card)));

        var otherView = SpincioEngine.ViewFor(step.State, new Seat(1));
        otherView.Declarations.ShouldHaveSingleItem()
            .ShouldBe(new DeclarationRecord(new Seat(0), 1, DeclarationKind.LowSumWithPair, 3));
        otherView.Hand.ShouldBe(Many("JC,NS,KB"));
    }

    [Fact]
    public void AT_20_declaration_on_second_deal()
    {
        var state = new Scenario { DealNumber = 2, PlaysInRound = 12 }.Hand(0, "AC,3S,5B").Build();

        var next = state.Ok(new Declare(new Seat(0))).State;

        next.Declarations.ShouldHaveSingleItem().DealNumber.ShouldBe(2);
        next.Score.A.ShouldBe(2);
    }

    [Fact]
    public void AT_20_new_deal_reopens_declarations()
    {
        // Last play of deal 1 by seat 3; the next deal gives seat 0 a fresh hand.
        var state = new Scenario { ToPlay = new Seat(3), PlaysInRound = 11 }
            .Hand(3, "KC")
            .Deck("AC,3S,5B,JC,NS,KB,2C,2S,5D,JB,NC,KS")
            .Build();

        var next = state.Ok(new PlayCard(new Seat(3), C("KC"))).State;

        next.DealNumber.ShouldBe(2);
        next.HandOf(new Seat(0)).ShouldBe(Many("AC,3S,5B"));
        next.ToPlay.ShouldBe(new Seat(0));
        SpincioEngine.LegalCommands(next, new Seat(0)).ShouldContain(new Declare(new Seat(0)));
    }

    private static MatchState PlayFirstCard(MatchState state) =>
        state.Ok(SpincioEngine.LegalCommands(state, state.ToPlay).OfType<PlayCard>().First()).State;
}
