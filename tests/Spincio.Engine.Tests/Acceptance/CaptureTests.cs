using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class CaptureTests
{
    private static string[] OptionsOf(string played, string table) =>
        [.. Captures.Options(C(played), Many(table)).Select(o => o.ToString())];

    [Fact]
    public void AT_05_equal_value_forbids_sum()
    {
        OptionsOf("5D", "5C,3S,2B").ShouldBe(["{5C}"]);
    }

    [Fact]
    public void AT_06_sum_capture()
    {
        OptionsOf("7D", "4C,3S,6B").ShouldBe(["{4C,3S}"]);
    }

    [Fact]
    public void AT_07_player_chooses_among_sums()
    {
        OptionsOf("7C", "AC,6S,2B,5D").ShouldBe([Canonical("{AC,6S}"), Canonical("{2B,5D}")], ignoreOrder: true);
    }

    [Fact]
    public void AT_08_king_captures_by_sum()
    {
        OptionsOf("KB", "7C,3S").ShouldBe([Canonical("{7C,3S}")]);
    }

    [Fact]
    public void AT_09_king_captures_face_card_plus_number()
    {
        OptionsOf("KD", "JC,2S").ShouldBe([Canonical("{JC,2S}")]);
    }

    [Fact]
    public void AT_10_two_equal_cards_take_one_of_them()
    {
        OptionsOf("5D", "5C,5S").ShouldBe(["{5C}", "{5S}"], ignoreOrder: true);
    }

    [Fact]
    public void AT_11_must_capture_only_with_the_card_played()
    {
        var state = new Scenario().Hand(0, "7D,KC").Table("3S,4B").Build();
        var seat = new Seat(0);

        SpincioEngine.Apply(state, new PlayCard(seat, C("KC"))).IsSuccess.ShouldBeTrue();
        SpincioEngine.Apply(state, new PlayCard(seat, C("7D"))).Error.ShouldBe(IllegalReason.MustCapture);
        SpincioEngine.Apply(state, new PlayCard(seat, C("7D"), Take("3S,4B"))).IsSuccess.ShouldBeTrue();

        SpincioEngine.LegalCommands(state, seat).ShouldBe(
            [new PlayCard(seat, C("7D"), Take("3S,4B")), new PlayCard(seat, C("KC"))], ignoreOrder: true);
    }

    [Fact]
    public void AT_12_card_that_cannot_capture_stays_on_table()
    {
        var state = new Scenario().Hand(0, "3S,KC").Table("6C").Build();

        var next = state.Ok(new PlayCard(new Seat(0), C("3S"))).State;

        next.Table.ShouldBe(Many("6C,3S"));
    }

    [Fact]
    public void C1_empty_table_any_card_is_dropped()
    {
        Captures.Options(C("7D"), []).ShouldBeEmpty();
    }

    [Fact]
    public void Capture_not_on_offer_is_rejected()
    {
        var state = new Scenario().Hand(0, "7C,KC").Table("AC,6S,2B,5D").Build();
        var seat = new Seat(0);

        SpincioEngine.Apply(state, new PlayCard(seat, C("7C"), Take("2B,5D,AC"))).Error.ShouldBe(IllegalReason.InvalidCapture);
        SpincioEngine.Apply(state, new PlayCard(seat, C("KC"), Take("AC"))).Error.ShouldBe(IllegalReason.CaptureNotAllowed);
        SpincioEngine.Apply(state, new PlayCard(seat, C("7B"))).Error.ShouldBe(IllegalReason.CardNotInHand);
    }

    [Fact]
    public void Captured_cards_and_played_card_go_to_the_team_pile()
    {
        var state = new Scenario().Hand(1, "7C,KC").Table("AC,6S,2B,5D").Build() with { ToPlay = new Seat(1) };

        var next = state.Ok(new PlayCard(new Seat(1), C("7C"), Take("2B,5D"))).State;

        next.PileOf(Team.B).ShouldBe(Many("2B,5D,7C"), ignoreOrder: true);
        next.Table.ShouldBe(Many("AC,6S"), ignoreOrder: true);
        next.LastCapturingTeam.ShouldBe(Team.B);
    }

    private static string Canonical(string option) => Take(option.Trim('{', '}')).ToString();
}
