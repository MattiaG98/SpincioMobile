using System.Globalization;
using System.Text;
using System.Text.Json;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Server.Feedback;

/// <summary>
/// A CPU move report as a GitHub issue (ADR 0020): what the player wrote, the position the CPU was in (replayed from the
/// match), and the report itself as JSON, ready for <c>Spincio.Simulator --report</c>.
/// </summary>
public static class CpuReportIssue
{
    public const string Label = "cpu-report";

    private static readonly JsonSerializerOptions Indented = new(SpincioJson.Options) { WriteIndented = true };

    /// <summary>Title and body of the issue. The report must be valid (<see cref="CpuMoveReports.Problem"/>).</summary>
    public static (string Title, string Body) Format(CpuMoveReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var (before, play) = Position(report);
        var seat = play.Seat;
        string summary = !string.IsNullOrWhiteSpace(report.Text)
            ? Shorten(report.Text.Trim(), 60)
            : string.Join(", ", report.Reasons.Select(r => CpuMoveReports.ReasonLabels[r]));
        string title = Defused($"CPU: {CommandCodec.Encode(play)} — {summary}");

        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"**Segnalazione su una mossa della CPU** (Spincio v{report.AppVersion}, regole v{report.RulesVersion})\n\n");
        if (!string.IsNullOrWhiteSpace(report.Text))
        {
            foreach (var line in report.Text.Trim().Split('\n'))
            {
                body.Append("> ").Append(Defused(line.TrimEnd())).Append('\n');
            }

            body.Append('\n');
        }

        if (report.Reasons.Count > 0)
        {
            body.Append("- **Cosa non va:** ").Append(string.Join(", ", report.Reasons.Select(r => CpuMoveReports.ReasonLabels[r]))).Append('\n');
        }

        if (report.BetterCard is { } better)
        {
            body.Append("- **Carta che avrebbe dovuto giocare:** ").Append(better).Append('\n');
        }

        body.Append(CultureInfo.InvariantCulture, $"- **Mossa segnalata:** `{CommandCodec.Encode(play)}` (posto {seat.Index}, mossa n. {report.Commands.Count})\n");
        body.Append(CultureInfo.InvariantCulture, $"- **Partita:** {report.Variant}, CPU {report.Level}, seme {report.Seed}, ");
        body.Append(CultureInfo.InvariantCulture, $"smazzata {before.RoundNumber}{(before.MatchNumber > 0 ? $" (spareggio {before.MatchNumber})" : "")}, distribuzione {before.DealNumber}\n");
        body.Append("- **Tavola prima della mossa:** ").Append(Cards(before.Table)).Append('\n');
        body.Append("- **Mano della CPU:** ").Append(Cards(before.HandOf(seat))).Append('\n');
        body.Append(CultureInfo.InvariantCulture, $"- **Punteggio:** {before.Score.A}–{before.Score.B}\n\n");
        body.Append("Per rigiocarla: salvare il JSON in un file e lanciare ");
        body.Append("`dotnet run --project tools/Spincio.Simulator -c Release -- --report file.json`.\n\n");
        body.Append("```json\n").Append(JsonSerializer.Serialize(report, Indented)).Append("\n```\n");
        return (title, body.ToString());
    }

    /// <summary>The state before the reported move, and the move.</summary>
    private static (MatchState Before, PlayCard Play) Position(CpuMoveReport report)
    {
        var state = SpincioEngine.NewMatch(report.Seed, variant: report.Variant).State;
        for (int i = 0; i < report.Commands.Count - 1; i++)
        {
            state = SpincioEngine.Apply(state, CommandCodec.Decode(report.Commands[i])).Value.State;
        }

        return (state, (PlayCard)CommandCodec.Decode(report.Commands[^1]));
    }

    private static string Cards(IEnumerable<Card> cards) => cards.Any() ? string.Join(" ", cards) : "(vuota)";

    private static string Shorten(string text, int length)
    {
        string line = text.ReplaceLineEndings(" ");
        return line.Length <= length ? line : line[..(length - 1)] + "…";
    }

    /// <summary>Keeps the player's words from pinging GitHub users or closing issues ("@name", "#12").</summary>
    private static string Defused(string text) => text.Replace("@", "@​", StringComparison.Ordinal).Replace("#", "#​", StringComparison.Ordinal);
}
