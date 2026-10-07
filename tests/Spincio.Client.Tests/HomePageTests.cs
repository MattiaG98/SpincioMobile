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

        AppInfo.Version.ShouldBe("1.0.0");
        page.Find("section.start .app-version").TextContent.ShouldBe("v1.0.0");
    }

    [Fact]
    public void Table_tells_the_layout_how_many_columns_each_row_count_needs()
    {
        var page = Render<Home>();
        page.Find("section.start button").Click();
        page.WaitForAssertion(() => page.FindAll(".hand .card").Count.ShouldBe(3));

        var n = page.FindAll(".table .card").Count;
        page.Find(".table").GetAttribute("style")
            .ShouldBe($"--c1: {n}; --c2: {(n + 1) / 2}; --c3: {(n + 2) / 3}; --c4: {(n + 3) / 4}");
    }
}
