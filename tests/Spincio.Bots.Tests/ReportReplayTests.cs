using System.Text.Json;
using Spincio.Contracts;
using Spincio.Engine;
using Spincio.Simulator;

namespace Spincio.Bots.Tests;

/// <summary>ADR 0020: a reported move is replayed and the CPU's reasons are shown next to the player's.</summary>
public class ReportReplayTests
{
    [Fact]
    public void Shows_how_the_cpu_weighed_each_play_and_marks_the_played_and_the_suggested_card()
    {
        // A real match played by L1, cut at the partner's first play with a real choice.
        var bot = new GreedyBot();
        var memories = Seat.All.Select(s => new BotMemory(s)).ToArray();
        var rng = new Pcg32(9, 1);
        var transition = SpincioEngine.NewMatch(9);
        var commands = new List<string>();
        string? better = null;
        while (better is null)
        {
            foreach (var memory in memories)
            {
                memory.Observe(transition.EventsFor(memory.Seat));
            }

            var state = transition.State;
            var seat = state.ToPlay;
            var command = bot.Choose(SpincioEngine.ViewFor(state, seat), memories[seat.Index], ref rng);
            commands.Add(CommandCodec.Encode(command));
            if (seat.Index == 2 && command is PlayCard play && SpincioEngine.LegalCommands(state, seat).OfType<PlayCard>().Select(p => p.Card).Distinct().Count() > 1)
            {
                better = state.HandOf(seat).First(c => c != play.Card).ToString();
                break;
            }

            transition = SpincioEngine.Apply(state, command).Value;
        }

        var report = new CpuMoveReport("1.14.0", "1.7", GameVariant.Spincio, "Normale", 9, commands, ["keep-card"], better, "Doveva tenerla");

        string text = ReportReplay.Explain(JsonSerializer.Serialize(report, SpincioJson.Options), worlds: 4);

        text.ShouldContain("<- played");
        text.ShouldContain("<- suggested");
        text.ShouldContain("\"Doveva tenerla\"");
        text.ShouldContain("Hand: ");
    }
}
