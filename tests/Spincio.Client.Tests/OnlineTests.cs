using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spincio.Client.Game;
using Spincio.Client.Pages;

namespace Spincio.Client.Tests;

/// <summary>ADR 0014: the online server comes from an invite link (or is typed in) and is remembered on the device.</summary>
public class OnlineTests : BunitContext
{
    public OnlineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<IOnlineSeatStore, InMemoryOnlineSeatStore>();
        Services.AddSingleton<OnlineService>();
        Services.AddSingleton(new GameHost(new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask)));
    }

    [Theory]
    [InlineData("https://spincio-abc-5080.app.github.dev", "https://spincio-abc-5080.app.github.dev/")]
    [InlineData("  https://example.org/some/path?x=1 ", "https://example.org/")]
    [InlineData("http://localhost:5080", "http://localhost:5080/")]
    [InlineData("http://example.org", null)] // not encrypted, not this computer
    [InlineData("ftp://example.org", null)]
    [InlineData("example.org", null)]
    [InlineData("", null)]
    public void Only_https_server_addresses_are_accepted(string address, string? expected)
    {
        string? parsed = OnlineService.Parse(address)?.ToString();
        parsed.ShouldBe(expected);
    }

    [Fact]
    public void The_invite_link_opens_the_online_page_on_the_server_with_the_room()
    {
        var link = OnlineService.InviteLink("https://mattiag98.github.io/SpincioMobile/", new Uri("https://spincio-abc-5080.app.github.dev/"), "K7QPZ");

        link.ShouldBe("https://mattiag98.github.io/SpincioMobile/online?server=https%3A%2F%2Fspincio-abc-5080.app.github.dev&room=K7QPZ");
    }

    [Fact]
    public void Without_a_server_the_page_asks_for_one()
    {
        var page = Render<Online>();

        page.Find("input[inputmode='url']").ShouldNotBeNull();
        page.Find("button.primary").HasAttribute("disabled").ShouldBeTrue();
        page.Find("input[inputmode='url']").Input("https://spincio-abc-5080.app.github.dev");
        page.Find("button.primary").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void An_invite_link_connects_to_its_server_and_fills_in_the_room()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/online?server=https%3A%2F%2Fspincio-abc-5080.app.github.dev&room=k7qpz");

        var page = Render<Online>();

        Services.GetRequiredService<OnlineService>().ServerUrl.ShouldBe(new Uri("https://spincio-abc-5080.app.github.dev/"));
        page.Find("input.code").GetAttribute("value").ShouldBe("K7QPZ");
        page.Find(".server-info").TextContent.ShouldContain("spincio-abc-5080.app.github.dev");
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe(["spincio.server", "https://spincio-abc-5080.app.github.dev/"]);
    }
}
