using Spincio.Client.Game;

namespace Spincio.Client.Tests;

/// <summary>The "Novità" panel: every version tells the players what changed.</summary>
public class ReleaseNotesTests
{
    /// <summary>A version bump without its note fails here (CLAUDE.md: version, CHANGELOG.md and ReleaseNotes go together).</summary>
    [Fact]
    public void The_current_version_has_its_note_on_top()
    {
        ReleaseNotes.Latest.Version.ShouldBe(AppInfo.Version);
    }

    [Fact]
    public void Notes_go_from_the_newest_version_to_the_oldest()
    {
        var versions = ReleaseNotes.All.Select(n => Version.Parse(n.Version)).ToList();

        versions.ShouldBe(versions.OrderDescending().ToList());
        versions.Distinct().Count().ShouldBe(versions.Count);
        ReleaseNotes.All.Select(n => n.Date).ShouldBe(ReleaseNotes.All.Select(n => n.Date).OrderDescending().ToList());
        ReleaseNotes.All[^1].Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void Every_note_says_something()
    {
        foreach (var note in ReleaseNotes.All)
        {
            note.Title.ShouldNotBeNullOrWhiteSpace();
            note.Changes.ShouldNotBeEmpty(note.Version);
            note.Changes.Where(string.IsNullOrWhiteSpace).ShouldBeEmpty(note.Version);
        }
    }

    [Theory]
    [InlineData(2026, 11, 13, "13 novembre 2026")]
    [InlineData(2026, 1, 2, "2 gennaio 2026")]
    public void Dates_are_written_in_italian(int year, int month, int day, string text)
    {
        new ReleaseNote("9.9.9", new DateOnly(year, month, day), "t", ["c"]).DateText.ShouldBe(text);
    }
}
