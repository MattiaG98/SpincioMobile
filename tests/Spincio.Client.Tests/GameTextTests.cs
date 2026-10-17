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

    [Fact]
    public void Each_cpu_has_its_own_avatar_next_to_its_name()
    {
        string?[] avatars = [.. Enumerable.Range(0, 4).Select(p => GameText.Avatar(GameText.AtPosition(Me, p), Me))];

        avatars.ShouldBe([null, "avatars/tito.svg", "avatars/titti.svg", "avatars/vava.svg"]);
        GameText.SeatName(GameText.AtPosition(Me, 2), Me).ShouldBe("Titti"); // same position, same player
    }

    [Fact]
    public void Seat_labels_show_only_the_declarations_of_the_deal_in_play()
    {
        var tito = GameText.AtPosition(Me, 1);
        var view = SpincioEngine.ViewFor(SpincioEngine.NewMatch(1).State, Me) with
        {
            DealNumber = 2,
            Declarations =
            [
                new DeclarationRecord(tito, 1, DeclarationKind.LowSum, 2),
                new DeclarationRecord(tito, 2, DeclarationKind.ThreeOfAKind, 7),
                new DeclarationRecord(Me, 2, DeclarationKind.LowSum, 2),
            ],
        };

        GameText.DeclarationsThisDeal(view, tito).ShouldHaveSingleItem().Points.ShouldBe(7);
    }

    /// <summary>SPEC §1.7: the Spincione's declarations add up, and first-hand ones say they are doubled.</summary>
    [Theory]
    [InlineData("AD,2D,3D", 3, "Mano fino a 9 + Colore + Scala")]
    [InlineData("NC,KS,AB", 3, "Scala")]
    [InlineData("2C,2S,5B", 3, "Mano fino a 9 con coppia")]
    [InlineData("3C,3S,3B,3D", 1, "Quattro uguali · prima mano ×2")]
    [InlineData("2C,2S,2B,4D", 1, "Mano fino a 10 con tre uguali · prima mano ×2")]
    [InlineData("AD,2C,3S,4B", 1, "Mano fino a 10 + Scala · prima mano ×2")]
    public void Spincione_declarations_have_readable_names(string hand, int deal, string name)
    {
        var value = Declarations.Evaluate([.. hand.Split(',').Select(Card.Parse)], GameVariant.Spincione, deal);

        GameText.DeclarationName(value.Kind).ShouldBe(name);
    }

    [Fact]
    public void A_capture_all_of_one_suit_is_announced()
    {
        var tito = GameText.AtPosition(Me, 1);
        var sameSuit = new CardPlayed(tito, Card.Parse("7D"), [Card.Parse("3D"), Card.Parse("4D")], IsSweep: false) { IsSameSuit = true };
        var both = sameSuit with { IsSweep = true };

        GameText.Banner(sameSuit).ShouldBe("Stesso seme!");
        GameText.Banner(both).ShouldBe("Spazzino!");
        GameText.Describe(sameSuit, Me, Name).ShouldEndWith(" — STESSO SEME! +1");
        GameText.Describe(both, Me, Name).ShouldEndWith(" — SPAZZINO! — STESSO SEME! +1");
        PointsGain.For(both, Me).ShouldNotBeNull().Points.ShouldBe(2);
        PointsGain.For(sameSuit, Me).ShouldNotBeNull().Label.ShouldBe("Stesso seme!");
    }

    /// <summary>SP10: a card taking its twin says "Gemella", and "Gemella con cappella" when it also empties the table.</summary>
    [Fact]
    public void A_twin_capture_is_announced()
    {
        var tito = GameText.AtPosition(Me, 1);
        var twin = new CardPlayed(tito, Card.Parse("5D"), [Card.Parse("5D")], IsSweep: false) { IsTwin = true };
        var withSweep = twin with { IsSweep = true };

        GameText.Banner(twin).ShouldBe("Gemella");
        GameText.Banner(withSweep).ShouldBe("Gemella con cappella");
        GameText.Describe(twin, Me, Name).ShouldEndWith(" — GEMELLA! +2");
        GameText.Describe(withSweep, Me, Name).ShouldEndWith(" — GEMELLA CON CAPPELLA! +3");
        PointsGain.For(twin, Me).ShouldNotBeNull().Points.ShouldBe(2);
        PointsGain.For(withSweep, Me).ShouldNotBeNull().Points.ShouldBe(3);
        PointsGain.For(withSweep, Me)!.Label.ShouldBe("Gemella con cappella");
    }

    [Fact]
    public void Identical_cards_get_distinct_keys()
    {
        GameText.Keyed([Card.Parse("7D"), Card.Parse("3C"), Card.Parse("7D")]).Select(k => k.Key).ShouldBe(["7D", "3C", "7D#2"]);
    }
}
