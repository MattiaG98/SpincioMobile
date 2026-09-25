using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

public class MatchEndTests
{
    [Fact]
    public void AT_27_reaching_31_mid_round_does_not_end_the_match()
    {
        var state = new Scenario { DealNumber = 2, PlaysInRound = 12, Score = new TeamScores(29, 0) }
            .Hand(0, "2C,2S,5B")
            .Build();

        var step = state.Ok(new Declare(new Seat(0)));

        step.State.Score.ShouldBe(new TeamScores(32, 0));
        step.State.Phase.ShouldBe(MatchPhase.AwaitingPlay);
        step.EventsOf<MatchEnded>().ShouldBeEmpty();
    }

    [Fact]
    public void AT_28_both_over_31_higher_wins()
    {
        MatchRules.Evaluate(new TeamScores(33, 32), allCoinsTeam: null)
            .ShouldBe(new MatchOutcome(MatchOutcomeKind.Win, Team.A, WinReason.Score));
    }

    [Fact]
    public void AT_29_first_to_31_wins()
    {
        MatchRules.Evaluate(new TeamScores(31, 20), allCoinsTeam: null)
            .ShouldBe(new MatchOutcome(MatchOutcomeKind.Win, Team.A, WinReason.Score));
        MatchRules.Evaluate(new TeamScores(30, 20), allCoinsTeam: null)
            .ShouldBe(new MatchOutcome(MatchOutcomeKind.Continue));
    }

    [Fact]
    public void AT_30_tie_at_31_or_more_starts_a_tiebreak()
    {
        MatchRules.Evaluate(new TeamScores(32, 32), allCoinsTeam: null)
            .ShouldBe(new MatchOutcome(MatchOutcomeKind.Tiebreak));
    }

    [Fact]
    public void AT_30_tiebreak_restarts_from_zero_and_repeats_while_tied()
    {
        var first = TiedLastPlay(matchNumber: 0).Ok(new PlayCard(new Seat(0), C("KC")));

        first.EventsOf<RoundScored>().ShouldHaveSingleItem().MatchScore.ShouldBe(new TeamScores(37, 37));
        first.EventsOf<TiebreakStarted>().ShouldHaveSingleItem().MatchNumber.ShouldBe(1);
        first.EventsOf<MatchEnded>().ShouldBeEmpty();
        first.State.MatchNumber.ShouldBe(1);
        first.State.RoundNumber.ShouldBe(1);
        first.State.Score.ShouldBe(TeamScores.Zero);
        first.State.Dealer.ShouldBe(new Seat(0)); // rotation continues (scenario dealer is seat 3)

        var second = TiedLastPlay(matchNumber: 1).Ok(new PlayCard(new Seat(0), C("KC")));

        second.EventsOf<TiebreakStarted>().ShouldHaveSingleItem().MatchNumber.ShouldBe(2);
    }

    [Fact]
    public void F7_all_coins_also_wins_a_tiebreak_match()
    {
        var state = new Scenario { ToPlay = new Seat(1), LastCapturingTeam = Team.B, MatchNumber = 1 }
            .LastPlay(Scenario.LeftoverTarget.PileB)
            .Pile(Team.A, Coins)
            .Hand(1, "KC")
            .Build();

        var step = state.Ok(new PlayCard(new Seat(1), C("KC")));

        step.EventsOf<MatchEnded>().ShouldHaveSingleItem().Reason.ShouldBe(WinReason.AllCoins);
    }

    [Fact]
    public void No_command_is_accepted_after_the_match_ends()
    {
        var steps = Playout.RandomMatch(seed: 30);
        var final = steps[^1].State;

        final.Phase.ShouldBe(MatchPhase.MatchOver);
        SpincioEngine.LegalCommands(final, final.ToPlay).ShouldBeEmpty();
        SpincioEngine.Apply(final, new Declare(final.ToPlay)).Error.ShouldBe(IllegalReason.MatchOver);
    }

    /// <summary>
    /// Round ends 2–2 from 35–35. A: rebello + settebello. B: more cards (21–19) + more coins (6–4).
    /// Sevens 2–2, no napola.
    /// </summary>
    private static MatchState TiedLastPlay(int matchNumber)
    {
        var pileA = Many("AD,5D,7D,KD,7C,AC,2C,3C,4C,5C,6C,JC,NC,AS,2S,3S,4S,5S,6S");
        pileA.Length.ShouldBe(19);

        return new Scenario
        {
            LastCapturingTeam = Team.B,
            Score = new TeamScores(35, 35),
            MatchNumber = matchNumber,
        }
            .LastPlay(Scenario.LeftoverTarget.PileB)
            .Pile(Team.A, pileA)
            .Hand(0, "KC")
            .Build();
    }
}
