using Spincio.Engine;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Bots.Tests;

public class GreedyBotTests
{
    private static Command Choose(PlayerView view, BotMemory memory)
    {
        var rng = Pcg32.FromSeed(1);
        return new GreedyBot().Choose(view, memory, ref rng);
    }

    [Fact]
    public void Always_declares_when_it_can()
    {
        var view = Views.For("2C,2S,5B", "KD", declarable: new DeclarationValue(DeclarationKind.LowSumWithPair, 3));

        Choose(view, Views.Memory("2C,2S,5B,KD")).ShouldBe(new Declare(new Seat(0)));
    }

    [Fact]
    public void Takes_a_sweep()
    {
        var view = Views.For("7C,KB,2S", "3S,4B");

        Choose(view, Views.Memory("7C,KB,2S,3S,4B")).ShouldBe(new PlayCard(new Seat(0), C("7C"), Take("3S,4B")));
    }

    [Fact]
    public void Prefers_the_settebello_over_more_coins()
    {
        // KB can take 5D+3D+2D (three coins) or 7D+3D / 7D+3C (settebello). Every option leaves a sum of 10.
        var view = Views.For("KB", "7D,3C,5D,3D,2D");
        var chosen = (PlayCard)Choose(view, Views.Memory("KB,7D,3C,5D,3D,2D"));

        chosen.Capture.ShouldNotBeNull().Cards.ShouldContain(RoundScoring.Settebello);
    }

    [Fact]
    public void Avoids_leaving_a_sweepable_table()
    {
        // Dropping 3S leaves 2B+3S (sweepable by any 5); dropping KC leaves 2B+KC (sum 12, not sweepable).
        var view = Views.For("KC,3S", "2B");

        Choose(view, Views.Memory("KC,3S,2B")).ShouldBe(new PlayCard(new Seat(0), C("KC")));
    }

    [Fact]
    public void Sweep_risk_is_zero_when_all_matching_cards_have_been_seen()
    {
        var view = Views.For("KC", "2B");
        var table = Many("2B,3S");

        GreedyBot.OpponentSweepProbability(table, view, Views.Memory("KC,2B,3S")).ShouldBeGreaterThan(0.1);
        GreedyBot.OpponentSweepProbability(table, view, Views.Memory("KC,2B,3S,5D,5C,5S,5B")).ShouldBe(0);
    }

    [Fact]
    public void Sweep_risk_is_zero_when_the_opponent_has_no_cards()
    {
        var view = Views.For("KC", "2B", handCounts: [1, 0, 1, 1]);

        GreedyBot.OpponentSweepProbability(Many("2B,3S"), view, Views.Memory("KC,2B")).ShouldBe(0);
    }

    [Fact]
    public void No_sweep_bonus_on_the_last_play()
    {
        var bot = new GreedyBot();
        var play = new PlayCard(new Seat(0), C("7C"), Take("3S,4B"));
        var memory = Views.Memory("7C,3S,4B");

        double normal = bot.Evaluate(play, Views.For("7C", "3S,4B", playsInRound: 20), memory);
        double last = bot.Evaluate(play, Views.For("7C", "3S,4B", playsInRound: MatchState.PlaysPerRound - 1), memory);

        (normal - last).ShouldBe(GreedyWeights.Default.Sweep);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("5C", 5)]
    [InlineData("KC", 10)]
    [InlineData("2B,3S", 5)]
    [InlineData("7C,3S", 10)]
    [InlineData("2B,3S,5C", 10)]
    [InlineData("KC,2B", null)]
    [InlineData("6C,5S", null)]
    public void Sweep_value(string table, int? expected)
    {
        GreedyBot.SweepValue(Many(table)).ShouldBe(expected);
    }
}
