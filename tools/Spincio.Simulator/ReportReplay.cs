using System.Globalization;
using System.Text;
using System.Text.Json;
using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Simulator;

/// <summary>
/// Replays a player's report about a CPU move (ADR 0020) and shows what the CPU knew and how it weighed each play, next to
/// the card the player suggested. The CPU is asked again with many worlds, so the values are steady.
/// </summary>
public static class ReportReplay
{
    public static string Explain(string json, int worlds)
    {
        var report = JsonSerializer.Deserialize<CpuMoveReport>(json, SpincioJson.Options)
            ?? throw new InvalidDataException("Not a report.");
        if (CpuMoveReports.Problem(report) is { } problem)
        {
            throw new InvalidDataException(problem);
        }

        var reported = (PlayCard)CommandCodec.Decode(report.Commands[^1]);
        var seat = reported.Seat;
        var memory = new BotMemory(seat, report.Variant);
        var transition = SpincioEngine.NewMatch(report.Seed, variant: report.Variant);
        memory.Observe(transition.EventsFor(seat));
        foreach (var command in report.Commands.Take(report.Commands.Count - 1))
        {
            transition = SpincioEngine.Apply(transition.State, CommandCodec.Decode(command)).Value;
            memory.Observe(transition.EventsFor(seat));
        }

        var state = transition.State;
        var view = SpincioEngine.ViewFor(state, seat);
        var rng = new Pcg32(report.Seed, 7);
        var bot = new LookaheadBot(LookaheadOptions.Default with { Worlds = worlds, NodeBudget = 0 });
        var values = bot.Explain(view, memory, ref rng);

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"{report.Variant}, CPU {report.Level}, app {report.AppVersion}, rules {report.RulesVersion}, seed {report.Seed}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Round {state.RoundNumber}, deal {state.DealNumber}, play {state.PlaysInRound + 1}, score {state.Score.A}-{state.Score.B}, seat {seat.Index} ({seat.Team})");
        text.AppendLine(CultureInfo.InvariantCulture, $"Hand: {string.Join(" ", view.Hand)}   Table: {string.Join(" ", view.Table)}   Unseen: {memory.UnseenCount}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Player: {string.Join(", ", report.Reasons)}{(report.BetterCard is { } b ? $"; better card {b}" : "")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"  \"{report.Text}\"");
        text.AppendLine(CultureInfo.InvariantCulture, $"L3 with {worlds} worlds (points for its team, best first):");
        foreach (var (play, value) in values)
        {
            string mark = play == reported ? "  <- played" : "";
            mark += report.BetterCard is { } better && play.Card.ToString() == better ? "  <- suggested" : "";
            text.AppendLine(CultureInfo.InvariantCulture, $"  {CommandCodec.Encode(play),-16} {value,7:+0.00;-0.00}{mark}");
        }

        return text.ToString();
    }
}
