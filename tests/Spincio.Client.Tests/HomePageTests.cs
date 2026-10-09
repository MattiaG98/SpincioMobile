using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spincio.Client.Game;
using Spincio.Client.Pages;

namespace Spincio.Client.Tests;

/// <summary>bUnit smoke tests (ADR 0006): the page renders and a card can be played by clicking it.</summary>
public class HomePageTests : BunitContext
{
    private readonly LocalGameSession _session = new(new InMemoryGameStore(), delay: _ => Task.CompletedTask);

    public HomePageTests()
    {
        Services.AddSingleton(_session);
        Services.AddSingleton(new GameHost(_session));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<SettingsService>();
        Services.AddSingleton<StatsService>();
        JSInterop.Mode = JSRuntimeMode.Loose; // card animations call into JS (js/moves.js)
    }

    [Fact]
    public void Online_link_is_hidden_without_a_server()
    {
        var page = Render<Home>();

        page.FindAll("a[href='online']").ShouldBeEmpty();
    }

    [Fact]
    public void Start_screen_offers_a_new_game()
    {
        var page = Render<Home>();

        page.Find("section.start").TextContent.ShouldContain("Nuova partita");
    }

    [Fact]
    public void New_game_shows_score_table_and_three_cards_in_hand()
    {
        var page = Render<Home>();

        page.Find("section.start button").Click();

        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));
        page.Find(".scorebar").TextContent.ShouldContain("Noi");
        page.Find(".table").ShouldNotBeNull();
    }

    [Fact]
    public void Clicking_a_playable_card_plays_it_or_asks_which_capture()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card.playable").Count.ShouldBeGreaterThan(0));

        page.FindAll(".hand .card.playable")[0].Click();

        page.WaitForAssertion(() =>
            (page.FindAll(".hand .card").Count < 3 || page.FindAll(".choice").Count > 1).ShouldBeTrue());
    }

    [Fact]
    public void CPU_players_have_human_names()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();

        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));
        var board = page.Find(".board").TextContent;
        board.ShouldContain(GameText.PartnerName);
        board.ShouldContain(GameText.RightOpponentName);
        board.ShouldContain(GameText.LeftOpponentName);
    }

    [Fact]
    public void Move_history_is_collapsed_and_shows_only_the_latest_line()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => _session.Feed.Count.ShouldBeGreaterThan(0));

        var feed = page.Find("details.feed");
        feed.HasAttribute("open").ShouldBeFalse();
        feed.QuerySelector("summary")!.TextContent.ShouldBe(_session.Feed[^1]);
        feed.QuerySelectorAll("li").Length.ShouldBe(_session.Feed.Count - 1);
    }

    [Fact]
    public void Leaving_needs_the_menu_and_a_confirmation()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));

        page.FindAll("button").Where(b => b.TextContent.Contains("Abbandona", StringComparison.Ordinal)).ShouldBeEmpty();
        page.Find("button[aria-label='Menu']").Click();
        page.FindAll("button").Single(b => b.TextContent.Contains("Abbandona la partita", StringComparison.Ordinal)).Click();
        page.Find("#menu-title").TextContent.ShouldBe("Abbandonare la partita?");
        page.FindAll("button").Single(b => b.TextContent.Contains("Sì, abbandona", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => page.FindAll("section.start").Count.ShouldBe(1));
    }

    [Fact]
    public void Options_change_the_cpu_level_shown_on_the_new_game_button()
    {
        var page = Render<Home>();

        page.FindAll("button").Single(b => b.TextContent.Contains("Opzioni", StringComparison.Ordinal)).Click();
        page.FindAll(".segmented button").Single(b => b.TextContent == "Difficile").Click();
        page.Find("button[aria-label='Chiudi']").Click();

        page.Find("section.start").TextContent.ShouldContain("CPU difficile");
    }

    [Fact]
    public void Rules_open_from_the_main_menu_and_from_the_pause_menu()
    {
        var page = Render<Home>();

        page.FindAll("button").Single(b => b.TextContent.Contains("Regole", StringComparison.Ordinal)).Click();
        page.Find("#rules-title").TextContent.ShouldBe("Regole dello Spincio");
        page.Find(".rules").TextContent.ShouldContain("Asso pigliatutto");
        page.FindAll("button").Single(b => b.TextContent == "Ho capito").Click();
        page.FindAll(".rules").ShouldBeEmpty();

        page.FindAll("section.start button").Single(b => b.TextContent.Contains("Nuova partita", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));
        page.Find("button[aria-label='Menu']").Click();
        page.FindAll("button").Single(b => b.TextContent.Contains("Regole", StringComparison.Ordinal)).Click();
        page.FindAll(".rules").Count.ShouldBe(1);
    }

    [Fact]
    public void Main_menu_shows_the_app_version()
    {
        var page = Render<Home>();

        AppInfo.Version.ShouldBe("1.5.0");
        page.Find("section.start .app-version").TextContent.ShouldBe("v1.5.0");
    }

    /// <summary>Seed 3: the CPUs empty the table before my first turn; seed 7: there are cards on it.</summary>
    [Theory]
    [InlineData("3", true)]
    [InlineData("7", false)]
    public void Table_tells_the_layout_how_many_columns_each_row_count_needs(string seed, bool emptyTable)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/?seed={seed}");
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));

        int count = page.FindAll(".table .card").Count;
        (count == 0).ShouldBe(emptyTable, "the deal for this seed changed: pick another seed");
        var n = Math.Max(count, 1); // an empty table is sized like one card
        page.Find(".table").GetAttribute("style")
            .ShouldBe($"--c1: {n}; --c2: {(n + 1) / 2}; --c3: {(n + 2) / 3}; --c4: {(n + 3) / 4}");
    }

    [Fact]
    public void End_of_round_points_reach_the_score_bar_when_the_summary_closes()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        for (int i = 0; i < 100 && _session.PendingSummary is null; i++)
        {
            page.WaitForAssertion(() => page.FindAll(".hand .card.playable").Count.ShouldBeGreaterThan(0));
            page.FindAll(".hand .card.playable")[0].Click();
            if (page.FindAll(".choice").Count > 0)
            {
                page.FindAll(".choice")[0].Click();
            }
        }

        var scored = _session.PendingSummary.ShouldNotBeNull();
        var us = _session.Me.Team;
        string[] Bar() => [.. page.FindAll(".scorebar .score strong").Select(s => s.TextContent)];
        var before = PointsGain.ScoreBefore(scored);
        page.WaitForAssertion(() => Bar().ShouldBe([$"{before.For(us)}", $"{before.For(GameText.Other(us))}"]));

        page.Find(".summary").ShouldNotBeNull();
        int callsBefore = JSInterop.Invocations["spincioMoves.points"].Count;
        page.FindAll(".overlay .btn-gold").Single(b => b.TextContent.Contains("Continua", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() =>
        {
            _session.PendingSummary.ShouldBeNull();
            Bar().ShouldBe([$"{_session.View.Score.For(us)}", $"{_session.View.Score.For(GameText.Other(us))}"]);
        });
        page.FindAll(".summary").ShouldBeEmpty();
        // The first points after the click are the end-of-round ones (then the CPUs may declare or sweep).
        var call = JSInterop.Invocations["spincioMoves.points"][callsBefore];
        call.Arguments.Count.ShouldBe(1); // one array with every team's points, not one argument per team
        ((System.Collections.IEnumerable)call.Arguments[0]!).Cast<object>().Count().ShouldBe(PointsGain.ForRound(scored, us).Count);
    }

    [Fact]
    public void Statistics_open_from_the_main_menu_and_count_an_abandoned_match()
    {
        var page = Render<Home>();
        page.FindAll("section.start button").Single(b => b.TextContent.Contains("Statistiche", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => page.Find("#stats-title").TextContent.ShouldBe("Statistiche"));
        page.Find(".stats").TextContent.ShouldContain("Gioca una partita");
        page.Find(".stats .btn-gold").Click();

        page.Find("section.start button").Click(); // new match
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));
        page.Find(".icon-btn").Click();
        page.FindAll(".overlay .btn").Single(b => b.TextContent.Contains("Abbandona", StringComparison.Ordinal)).Click();
        page.FindAll(".overlay .btn").Single(b => b.TextContent.Contains("Sì, abbandona", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => page.Find("section.start").ShouldNotBeNull());
        page.FindAll("section.start button").Single(b => b.TextContent.Contains("Statistiche", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => page.Find(".stats").TextContent.ShouldContain("Abbandonate"));
        page.FindAll(".stat-tile").Single(t => t.TextContent.Contains("Abbandonate", StringComparison.Ordinal))
            .QuerySelector("strong")!.TextContent.ShouldBe("1");
    }

    [Fact]
    public void Credits_open_from_the_main_menu_and_list_the_contributors()
    {
        var page = Render<Home>();

        page.FindAll("section.start button").Single(b => b.TextContent.Contains("Contributi", StringComparison.Ordinal)).Click();

        page.Find("#credits-title").TextContent.ShouldBe("Contributi");
        page.FindAll(".credits-people li").Select(li => li.TextContent).ShouldBe(["Megako", "Nandone"]);
        page.Find(".credits .btn-gold").Click();
        page.FindAll(".credits").ShouldBeEmpty();
    }

    [Fact]
    public void Seats_show_each_cpu_avatar_with_a_team_ring()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));

        string Avatar(int position) => page.Find($".seat-{position} img.avatar").GetAttribute("src")!;
        Avatar(1).ShouldBe("avatars/tito.svg");
        Avatar(2).ShouldBe("avatars/titti.svg");
        Avatar(3).ShouldBe("avatars/vava.svg");
        page.Find(".seat-2 .avatar").ClassList.ShouldContain("ours");
        page.Find(".seat-1 .avatar").ClassList.ShouldContain("theirs");
        page.Find(".seat-3 .avatar").ClassList.ShouldContain("theirs");
    }
}
