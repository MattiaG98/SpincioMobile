using System.Collections.Immutable;
using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

/// <summary>Which points fly to which score: sweeps and declarations live, the rest when the round summary closes.</summary>
public class PointsGainTests
{
    private static readonly Seat Me = new(0);

    private static TeamTally Tally(int cards = 0, int coins = 0, int rebello = 0, int settebello = 0, int primiera = 0, int spincio = 0) =>
        new(20, 5, 2, 0, cards, coins, rebello, settebello, primiera, spincio);

    [Fact]
    public void A_sweep_is_one_point_from_the_table_to_the_sweepers_team()
    {
        var sweep = new CardPlayed(new Seat(1), new Card(Rank.Five, Suit.Swords), [new Card(Rank.Five, Suit.Cups)], IsSweep: true);

        PointsGain.For(sweep, Me).ShouldBe(new PointsGain(PointsKind.Sweep, PointsGain.Table, Ours: false, 1, "Spazzino!"));
    }

    [Fact]
    public void A_declaration_flies_from_the_declaring_seat_with_its_name()
    {
        var declared = new Declared(new Seat(2), DeclarationKind.ThreeOfAKind, 7);

        PointsGain.For(declared, Me).ShouldBe(new PointsGain(PointsKind.Declaration, 2, Ours: true, 7, "Tris"));
    }

    [Fact]
    public void Plays_that_score_nothing_have_no_points()
    {
        var drop = new CardPlayed(Me, new Card(Rank.Two, Suit.Coins), [], IsSweep: false);
        var capture = new CardPlayed(Me, new Card(Rank.Two, Suit.Coins), [new Card(Rank.Two, Suit.Clubs)], IsSweep: false);

        PointsGain.For(drop, Me).ShouldBeNull();
        PointsGain.For(capture, Me).ShouldBeNull();
        PointsGain.For(new RoundStarted(0, 2, Me), Me).ShouldBeNull();
    }

    [Fact]
    public void End_of_round_points_go_to_each_team_that_scored_ours_first()
    {
        var scored = new RoundScored(
            new RoundScore(Tally(cards: 1, rebello: 1), Tally(coins: 1, settebello: 1, spincio: 3), null),
            RoundLedger.Empty,
            new TeamScores(12, 20));

        PointsGain.ForRound(scored, Team.B).ShouldBe([
            new PointsGain(PointsKind.RoundEnd, PointsGain.Table, Ours: true, 5, null),
            new PointsGain(PointsKind.RoundEnd, PointsGain.Table, Ours: false, 2, null),
        ]);
        PointsGain.ScoreBefore(scored).ShouldBe(new TeamScores(10, 15));
    }

    [Fact]
    public void A_team_without_end_of_round_points_gets_no_pop()
    {
        var scored = new RoundScored(new RoundScore(Tally(), Tally(cards: 1, coins: 1), null), RoundLedger.Empty, new TeamScores(3, 9));

        PointsGain.ForRound(scored, Team.A).ShouldBe([new PointsGain(PointsKind.RoundEnd, PointsGain.Table, Ours: false, 2, null)]);
    }

    [Fact]
    public void A_mariana_shows_its_name_with_the_sweep_point_or_with_no_points()
    {
        var mariana = new CardPlayed(new Seat(2), Card.Parse("JD"), [Card.Parse("5C"), Card.Parse("3S")], IsSweep: false);

        PointsGain.For(mariana, Me).ShouldBe(new PointsGain(PointsKind.Mariana, PointsGain.Table, Ours: true, 0, "MARIANA"));
        PointsGain.For(mariana with { IsSweep = true }, Me).ShouldBe(new PointsGain(PointsKind.Sweep, PointsGain.Table, Ours: true, 1, "MARIANA"));
    }
}
