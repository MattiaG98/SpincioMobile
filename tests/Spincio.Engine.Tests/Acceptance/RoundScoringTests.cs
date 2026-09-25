using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class RoundScoringTests
{
    /// <summary>Splits the deck: team A gets <paramref name="a"/>, team B gets everything else.</summary>
    private static RoundScore ScoreWithPileA(IEnumerable<Card> a)
    {
        var pileA = a.ToList();
        var pileB = Card.FullDeck.Except(pileA).ToList();
        return RoundScoring.Score(pileA, pileB);
    }

    private static IEnumerable<Card> NonCoinsWithoutSevens => Card.FullDeck.Where(c => !c.IsCoins && c.Rank != Rank.Seven);

    [Fact]
    public void AT_21_more_cards_scores_one_tie_scores_zero()
    {
        var a21 = RoundScoring.Score(Card.FullDeck.Take(21).ToList(), Card.FullDeck.Skip(21).Take(19).ToList());
        a21.A.CardsPoint.ShouldBe(1);
        a21.B.CardsPoint.ShouldBe(0);

        var tie = RoundScoring.Score(Card.FullDeck.Take(20).ToList(), Card.FullDeck.Skip(20).ToList());
        tie.A.CardsPoint.ShouldBe(0);
        tie.B.CardsPoint.ShouldBe(0);
    }

    [Fact]
    public void AT_22_more_coins_scores_one_tie_scores_zero()
    {
        var sixFour = ScoreWithPileA(Many("2D,3D,4D,5D,6D,JD"));
        sixFour.A.CoinsPoint.ShouldBe(1);
        sixFour.B.CoinsPoint.ShouldBe(0);

        var fiveFive = ScoreWithPileA(Many("2D,3D,4D,5D,6D"));
        fiveFive.A.CoinsPoint.ShouldBe(0);
        fiveFive.B.CoinsPoint.ShouldBe(0);
    }

    [Fact]
    public void AT_23_rebello_and_settebello()
    {
        var score = ScoreWithPileA(Many("KD,7D"));

        score.A.RebelloPoint.ShouldBe(1);
        score.A.SettebelloPoint.ShouldBe(1);
        score.B.RebelloPoint.ShouldBe(0);
        score.B.SettebelloPoint.ShouldBe(0);
    }

    [Fact]
    public void AT_24_primiera_is_most_sevens()
    {
        var threeOne = ScoreWithPileA(Many("7D,7C,7S"));
        threeOne.A.PrimieraPoint.ShouldBe(1);
        threeOne.B.PrimieraPoint.ShouldBe(0);

        var twoTwo = ScoreWithPileA(Many("7D,7C"));
        twoTwo.A.PrimieraPoint.ShouldBe(0);
        twoTwo.B.PrimieraPoint.ShouldBe(0);
    }

    [Theory]
    [InlineData("AD,2D,3D", 3)]
    [InlineData("AD,2D,3D,4D,5D", 5)]
    [InlineData("AD,2D,4D", 0)]
    [InlineData("2D,3D,4D", 0)]
    [InlineData("AD,2D,3D,4D,5D,6D,7D,JD,ND", 9)]
    [InlineData("AD,2D,3C,4D", 0)]
    public void AT_25_napola(string pile, int points)
    {
        RoundScoring.NapolaLength(Many(pile)).ShouldBe(points);
    }

    [Fact]
    public void All_points_cumulate_seven_of_coins_counts_everywhere()
    {
        var score = ScoreWithPileA(Many("AD,2D,3D,4D,5D,6D,7D").Concat(NonCoinsWithoutSevens.Take(14)));

        score.A.ShouldBe(new TeamTally(
            CardCount: 21, CoinCount: 7, SevenCount: 1, NapolaLength: 7,
            CardsPoint: 1, CoinsPoint: 1, RebelloPoint: 0, SettebelloPoint: 1, PrimieraPoint: 0, NapolaPoints: 7));
        score.A.Total.ShouldBe(10);
    }

    [Fact]
    public void AT_26_all_ten_coins_wins_even_when_behind()
    {
        var state = new Scenario { ToPlay = new Seat(1), LastCapturingTeam = Team.B, Score = new TeamScores(0, 30) }
            .LastPlay(Scenario.LeftoverTarget.PileB)
            .Pile(Team.A, Coins)
            .Hand(1, "KC")
            .Build();

        var step = state.Ok(new PlayCard(new Seat(1), C("KC")));

        var scored = step.EventsOf<RoundScored>().ShouldHaveSingleItem();
        scored.Score.AllCoinsTeam.ShouldBe(Team.A);
        scored.MatchScore.B.ShouldBeGreaterThan(scored.MatchScore.A);
        scored.MatchScore.B.ShouldBeGreaterThanOrEqualTo(MatchRules.TargetScore);
        step.EventsOf<MatchEnded>().ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Winner.ShouldBe(Team.A),
            e => e.Reason.ShouldBe(WinReason.AllCoins));
        step.State.Phase.ShouldBe(MatchPhase.MatchOver);
        step.State.Winner.ShouldBe(Team.A);
    }
}
