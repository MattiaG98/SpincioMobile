using FsCheck.Xunit;
using Spincio.Engine;
using Spincio.Simulator;

namespace Spincio.Bots.Tests;

public class SimulationTests
{
    /// <summary>M2 acceptance criterion (ADR 0005): L1 beats L0 in at least 80% of matches.</summary>
    [Fact]
    public void L1_beats_L0_in_at_least_80_percent_of_matches()
    {
        var result = Tournament.Run(new GreedyBot(), new RandomBot(), matches: 400, seed: 2026);

        result.WinRateX.ShouldBeGreaterThanOrEqualTo(0.80, result.ToString());
    }

    [Fact]
    public void Mirror_match_has_no_side_bias()
    {
        var result = Tournament.Run(new GreedyBot(), new GreedyBot(), matches: 400, seed: 7);

        result.WinRateX.ShouldBeInRange(0.40, 0.60, result.ToString());
    }

    /// <summary>Any illegal bot command makes <see cref="MatchRunner"/> throw.</summary>
    [Property(MaxTest = 30)]
    public void Bots_only_play_legal_commands_and_matches_end(ulong seed)
    {
        IBot l0 = new RandomBot(), l1 = new GreedyBot();

        MatchRunner.Play(seed, [l1, l0, l1, l0]).Commands.ShouldBeGreaterThan(0);
        MatchRunner.Play(seed, [l0, l1, l0, l1]).Commands.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Same_seed_same_bot_match()
    {
        IBot l1 = new GreedyBot();

        var first = MatchRunner.Play(42, [l1, l1, l1, l1]);
        var second = MatchRunner.Play(42, [l1, l1, l1, l1]);

        second.ShouldBe(first);
    }

    /// <summary>Checks every decision: whenever the seat could declare (A3), the bot's choice is <see cref="Declare"/>.</summary>
    private sealed class DeclarationAudit(IBot inner) : IBot
    {
        public int Chances { get; private set; }

        public int Missed { get; private set; }

        public string Name => inner.Name;

        public Command Choose(PlayerView view, BotMemory memory, ref Pcg32 rng)
        {
            var command = inner.Choose(view, memory, ref rng);
            if (view.AvailableDeclaration.Points > 0)
            {
                Chances++;
                Missed += command is Declare ? 0 : 1;
            }

            return command;
        }
    }

    [Fact]
    public void Bots_declare_every_time_they_can_over_whole_matches()
    {
        var l1 = new DeclarationAudit(new GreedyBot());
        var l2 = new DeclarationAudit(new PimcBot(PimcOptions.Default with { Worlds = 2 }));

        for (ulong seed = 1; seed <= 10; seed++)
        {
            MatchRunner.Play(seed, [l1, l2, l1, l2]);
        }

        l1.Chances.ShouldBeGreaterThan(10);
        l2.Chances.ShouldBeGreaterThan(10);
        l1.Missed.ShouldBe(0);
        l2.Missed.ShouldBe(0);
    }

    [Fact]
    public void Bots_get_only_their_own_private_events()
    {
        var start = SpincioEngine.NewMatch(3);

        foreach (var seat in Seat.All)
        {
            var memory = new BotMemory(seat);
            memory.Observe(start.EventsFor(seat)); // throws if an event is addressed to another seat
            memory.Seen.ShouldBe(start.State.HandOf(seat).Concat(start.State.Table), ignoreOrder: true);
        }
    }
}
