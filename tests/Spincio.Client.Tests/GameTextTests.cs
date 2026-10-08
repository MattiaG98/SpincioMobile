using System.Collections.Immutable;
using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

public class GameTextTests
{
    private static readonly Seat Me = new(0);

    private static string Name(Seat seat) => GameText.SeatName(seat, Me);

    [Fact]
    public void Offline_seats_use_the_owner_chosen_names()
    {
        GameText.SeatName(Me, Me).ShouldBe("Tu");
        GameText.SeatName(GameText.AtPosition(Me, 1), Me).ShouldBe("Tito");
        GameText.SeatName(GameText.AtPosition(Me, 2), Me).ShouldBe("Titti");
        GameText.SeatName(GameText.AtPosition(Me, 3), Me).ShouldBe("Vava");
    }

    [Fact]
    public void History_speaks_to_the_viewer_in_the_second_person()
    {
        var capture = new CardPlayed(Me, Card.Parse("JB"), [Card.Parse("JS")], IsSweep: false);
        var drop = new CardPlayed(Me, Card.Parse("5D"), ImmutableArray<Card>.Empty, IsSweep: false);

        GameText.Describe(capture, Me, Name).ShouldBe("Tu prendi Fante di spade con Fante di bastoni");
        GameText.Describe(drop, Me, Name).ShouldBe("Tu cali 5 di denari");
        GameText.Describe(new Declared(Me, DeclarationKind.ThreeOfAKind, 3), Me, Name).ShouldStartWith("Tu accusi: Tris");
    }

    [Fact]
    public void History_uses_the_third_person_for_the_other_players()
    {
        var partner = GameText.AtPosition(Me, 2);
        var drop = new CardPlayed(partner, Card.Parse("3S"), ImmutableArray<Card>.Empty, IsSweep: false);

        GameText.Describe(drop, Me, Name).ShouldBe("Titti cala 3 di spade");
    }

    [Fact]
    public void Mariana_takes_the_place_of_the_sweep_call_and_also_shows_without_a_sweep()
    {
        var tito = GameText.AtPosition(Me, 1);
        var mariana = new CardPlayed(tito, Card.Parse("JD"), [Card.Parse("5C"), Card.Parse("3S")], IsSweep: false);
        var sweep = new CardPlayed(tito, Card.Parse("7D"), [Card.Parse("3S"), Card.Parse("4B")], IsSweep: true);

        GameText.Describe(mariana, Me, Name).ShouldBe("Tito prende 5 di coppe + 3 di spade con Fante di denari — MARIANA!");
        GameText.Describe(mariana with { IsSweep = true }, Me, Name).ShouldEndWith(" — MARIANA! +1");
        GameText.Describe(sweep, Me, Name).ShouldEndWith(" — SPAZZINO!");
        GameText.Banner(mariana).ShouldBe("MARIANA");
        GameText.Banner(mariana with { IsSweep = true }).ShouldBe("MARIANA");
        GameText.Banner(sweep).ShouldBe("Spazzino!");
        GameText.Banner(sweep with { IsSweep = false }).ShouldBeNull();
    }
}
