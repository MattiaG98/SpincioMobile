using Bunit;
using Spincio.Client.Components;
using Spincio.Engine;

namespace Spincio.Client.Tests;

public class RoundSummaryTests : BunitContext
{
    private static RoundScored Scored(RoundLedger ledger) => new(
        RoundScoring.Score(Card.FullDeck, [], GameVariant.Spincione), ledger, new TeamScores(10, 4));

    /// <summary>SP10: the twins' points are listed with the other live points, only when someone took a twin.</summary>
    [Fact]
    public void Lists_the_twins_only_when_there_are_some()
    {
        var withTwins = Render<RoundSummary>(p => p
            .Add(s => s.Scored, Scored(RoundLedger.Empty with { Twins = new TeamScores(2, 4) }))
            .Add(s => s.Us, Team.B));

        var row = withTwins.FindAll("tr.live").Single(r => r.TextContent.Contains("Gemelle", StringComparison.Ordinal));
        row.QuerySelectorAll("td").Select(td => td.TextContent).ShouldBe(["Gemelle (già contate)", "4", "2"]);

        var without = Render<RoundSummary>(p => p.Add(s => s.Scored, Scored(RoundLedger.Empty)));
        without.Markup.ShouldNotContain("Gemelle");
    }
}
