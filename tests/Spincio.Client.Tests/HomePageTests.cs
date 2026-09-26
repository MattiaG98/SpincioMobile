using Bunit;
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
}
