using Spincio.Engine;

namespace Spincio.Contracts;

/// <summary>
/// A player's report about a CPU move it thinks was wrong (ADR 0020). The match travels as seed + commands (ADR 0003):
/// <see cref="Commands"/> ends with the reported move, so anyone can replay the very position the CPU was in.
/// It holds the cards and moves of the match and what the player wrote, nothing about who the player is.
/// </summary>
/// <param name="Level">CPU level as the player chose it ("Normale", "Difficile").</param>
/// <param name="Reasons">Quick answers to "what was wrong" (<see cref="CpuMoveReports.ReasonKeys"/>).</param>
/// <param name="BetterCard">The card the player thinks the CPU should have played (e.g. "7D"), if given.</param>
/// <param name="Text">How the CPU should have reasoned, in the player's words.</param>
public sealed record CpuMoveReport(
    string AppVersion,
    string RulesVersion,
    GameVariant Variant,
    string Level,
    ulong Seed,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> Reasons,
    string? BetterCard,
    string Text);

/// <summary>What a report must look like to be accepted: the same checks on the phone and on the server.</summary>
public static class CpuMoveReports
{
    public const int MaxTextLength = 500;

    /// <summary>A match never gets near this (a Spincione to 101 with tiebreaks is about 1,500 commands).</summary>
    public const int MaxCommands = 5_000;

    /// <summary>The quick answers to "what was wrong", by key, with the words the app shows (also used in the issue).</summary>
    public static IReadOnlyDictionary<string, string> ReasonLabels { get; } = new Dictionary<string, string>
    {
        ["sweep"] = "Doveva fare spazzino",
        ["gave-sweep"] = "Ha lasciato lo spazzino",
        ["take-more"] = "Poteva prendere di meglio",
        ["keep-card"] = "Doveva tenersi la carta",
        ["other"] = "Altro",
    };

    public static IReadOnlyList<string> ReasonKeys { get; } = [.. ReasonLabels.Keys];

    /// <summary>
    /// Null if the report is acceptable, otherwise why not. It replays the match: every command must be legal, the last
    /// one a CPU's play (not seat 0, the player's own seat offline), and the better card one the CPU held then.
    /// </summary>
    public static string? Problem(CpuMoveReport? report)
    {
        if (report is null)
        {
            return "Empty report.";
        }

        if (report.Commands is null || report.Commands.Count is 0 or > MaxCommands)
        {
            return "The match must have between 1 and 5000 commands.";
        }

        if (!Enum.IsDefined(report.Variant))
        {
            return "Unknown mode.";
        }

        if (report.Text is null || report.Text.Length > MaxTextLength)
        {
            return $"The text must be at most {MaxTextLength} characters.";
        }

        if (report.Reasons is null || report.Reasons.Count > ReasonKeys.Count || report.Reasons.Any(r => !ReasonKeys.Contains(r)))
        {
            return "Unknown reason.";
        }

        if (string.IsNullOrWhiteSpace(report.Text) && report.Reasons.Count == 0 && report.BetterCard is null)
        {
            return "The report says nothing.";
        }

        if (BadLabel(report.AppVersion) || BadLabel(report.RulesVersion) || BadLabel(report.Level))
        {
            return "Versions and level must be short texts.";
        }

        var state = SpincioEngine.NewMatch(report.Seed, variant: report.Variant).State;
        for (int i = 0; i < report.Commands.Count; i++)
        {
            if (!CommandCodec.TryDecode(report.Commands[i], out var command))
            {
                return $"Command {i} is not a command.";
            }

            if (i == report.Commands.Count - 1)
            {
                return Last(state, command!, report.BetterCard);
            }

            var result = SpincioEngine.Apply(state, command!);
            if (!result.IsSuccess)
            {
                return $"Command {i} is not legal: {result.Error}.";
            }

            state = result.Value.State;
        }

        return null;
    }

    private static string? Last(MatchState state, Command command, string? betterCard)
    {
        if (command is not PlayCard play || play.Seat.Index == 0)
        {
            return "The last command must be a CPU's play.";
        }

        if (!SpincioEngine.Apply(state, command).IsSuccess)
        {
            return "The reported move is not legal.";
        }

        if (betterCard is not null && (!Card.TryParse(betterCard, out var card) || !state.HandOf(play.Seat).Contains(card)))
        {
            return "The better card was not in the CPU's hand.";
        }

        return null;
    }

    private static bool BadLabel(string? text) => string.IsNullOrWhiteSpace(text) || text.Length > 20;
}
