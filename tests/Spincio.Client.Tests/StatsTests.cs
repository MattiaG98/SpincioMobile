using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

/// <summary>Statistics: what each event counts, and that a resumed match is not counted twice.</summary>
public class StatsTests
{
    private static readonly Seat Me = new(0);

    private static TeamTally Tally(int settebello = 0, int rebello = 0, int spincio = 0, int cards = 0) =>
        new(20, 5, 2, spincio, cards, 0, rebello, settebello, 0, spincio);

    private static PlayerStats Apply(PlayerStats stats, StatsMode mode, params GameEvent[] events) =>
        StatsTracker.Apply(stats, events, Me, mode);

    [Fact]
    public void Counts_my_sweeps_and_declarations_not_the_others()
    {
        var sweep = new CardPlayed(Me, new Card(Rank.Five, Suit.Coins), [new Card(Rank.Five, Suit.Cups)], IsSweep: true);
        var partnerSweep = sweep with { Seat = new Seat(2) };
        var capture = sweep with { IsSweep = false };

        var stats = Apply(new PlayerStats(), StatsMode.Normal,
            sweep, partnerSweep, capture, new Declared(Me, DeclarationKind.ThreeOfAKind, 7), new Declared(new Seat(1), DeclarationKind.LowSum, 2));

        stats.MySweeps.ShouldBe(1);
        stats.MyDeclarations.ShouldBe(1);
        stats.MyDeclarationPoints.ShouldBe(7);
    }

    [Fact]
    public void A_round_adds_my_team_points_and_its_trophies()
    {
        var scored = new RoundScored(
            new RoundScore(Tally(settebello: 1, spincio: 4, cards: 1), Tally(rebello: 1), null),
            new RoundLedger(new TeamScores(2, 1), new TeamScores(3, 0)),
            new TeamScores(15, 2));

        var stats = Apply(new PlayerStats(), StatsMode.Normal, scored);

        stats.Rounds.ShouldBe(1);
        stats.TeamPoints.ShouldBe(6 + 2 + 3); // end of round (settebello, spincio 4, cards) + sweeps + declarations
        stats.Settebelli.ShouldBe(1);
        stats.Rebelli.ShouldBe(0);
        stats.SpincioRounds.ShouldBe(1);
        stats.LongestSpincio.ShouldBe(4);
    }

    [Fact]
    public void Match_results_go_to_their_mode_with_streaks_and_records()
    {
        var won = new MatchEnded(Team.A, WinReason.Score, new TeamScores(34, 20));
        var lost = new MatchEnded(Team.B, WinReason.Score, new TeamScores(25, 31));
        var allCoins = new MatchEnded(Team.A, WinReason.AllCoins, new TeamScores(12, 18));

        var stats = Apply(new PlayerStats(), StatsMode.Hard, won);
        stats = Apply(stats, StatsMode.Hard, won with { FinalScore = new TeamScores(31, 29) });
        stats = Apply(stats, StatsMode.Online, lost);
        stats = Apply(stats, StatsMode.Normal, allCoins);

        stats.Hard.ShouldBe(new ModeStats(2, 2));
        stats.Online.ShouldBe(new ModeStats(1, 0));
        stats.Normal.ShouldBe(new ModeStats(1, 1));
        stats.Played.ShouldBe(4);
        stats.Won.ShouldBe(3);
        stats.BestStreak.ShouldBe(2);
        stats.CurrentStreak.ShouldBe(1);
        stats.BiggestWinMargin.ShouldBe(14);
        stats.AllCoinsWins.ShouldBe(1);
    }

    [Fact]
    public void Abandoning_counts_and_breaks_the_streak()
    {
        var stats = Apply(new PlayerStats(), StatsMode.Normal, new MatchEnded(Team.A, WinReason.Score, new TeamScores(31, 3)));

        stats = StatsTracker.Abandon(stats);

        stats.Abandoned.ShouldBe(1);
        stats.CurrentStreak.ShouldBe(0);
        stats.BestStreak.ShouldBe(1);
        stats.Played.ShouldBe(1);
    }

    [Fact]
    public async Task Live_moves_are_reported_once_and_a_resumed_match_is_not_replayed()
    {
        var store = new InMemoryGameStore();
        var first = new LocalGameSession(store, delay: _ => Task.CompletedTask);
        var live = new List<GameEvent>();
        first.LiveEvents += live.AddRange;
        await first.NewGameAsync(seed: 31);
        var play = first.LegalCommands.OfType<PlayCard>().First();
        await first.PlayAsync(play);

        live.OfType<CardPlayed>().Count(p => p.Seat == Me).ShouldBe(1);
        live.OfType<HandDealt>().ShouldNotBeEmpty();

        var resumed = new LocalGameSession(store, delay: _ => Task.CompletedTask);
        var replayed = new List<GameEvent>();
        resumed.LiveEvents += replayed.AddRange;
        (await resumed.TryResumeAsync()).ShouldBeTrue();

        replayed.ShouldBeEmpty();
    }
}
