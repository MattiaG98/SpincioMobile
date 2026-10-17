using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spincio.Client.Game;
using Spincio.Client.Pages;
using Spincio.Contracts;

namespace Spincio.Client.Tests;

/// <summary>ADR 0014: the online server comes from an invite link (or is typed in) and is remembered on the device.</summary>
public class OnlineTests : BunitContext
{
    private const string Server = "https://spincio-abc-5080.app.github.dev/";
    private readonly FakeLobby _lobby = new();

    public OnlineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<IOnlineSeatStore, InMemoryOnlineSeatStore>();
        Services.AddSingleton<OnlineService>();
        Services.AddSingleton<ProfileService>();
        Services.AddSingleton<SettingsService>();
        Services.AddSingleton<ILobby>(_lobby);
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
        JSInterop.Invocations["localStorage.setItem"].ShouldContain(i => i.Arguments.SequenceEqual(new object?[] { "spincio.server", Server }));
    }

    private void UseBuiltInServer() =>
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ServerUrl"] = "https://spincio-server.onrender.com" })
            .Build());

    /// <summary>ADR 0016: with the server built into the app, online play needs no link and nothing to change.</summary>
    [Fact]
    public void The_built_in_server_is_used_without_a_link()
    {
        UseBuiltInServer();

        var page = Render<Online>();

        Services.GetRequiredService<OnlineService>().ServerUrl.ShouldBe(new Uri("https://spincio-server.onrender.com/"));
        page.FindAll("input[inputmode='url']").ShouldBeEmpty();
        page.Find("input[placeholder='Nome']").ShouldNotBeNull();
        page.Find(".server-info").TextContent.ShouldContain("spincio-server.onrender.com");
        page.FindAll(".server-info button").ShouldBeEmpty();
    }

    [Fact]
    public void A_server_from_an_invite_link_can_be_dropped_for_the_built_in_one()
    {
        UseBuiltInServer();
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.player").SetResult("""{"Id":"ada1","Name":"Ada"}""");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/online?server=https%3A%2F%2Fspincio-abc-5080.app.github.dev");
        var page = Render<Online>();
        page.Find(".server-info").TextContent.ShouldContain("spincio-abc-5080.app.github.dev");

        page.Find(".server-info button").Click();

        Services.GetRequiredService<OnlineService>().ServerUrl.ShouldBe(new Uri("https://spincio-server.onrender.com/"));
        page.Find(".server-info").TextContent.ShouldContain("spincio-server.onrender.com");
        page.FindAll(".server-info button").ShouldBeEmpty();
        _lobby.Starts[^1].Server.ShouldBe(new Uri("https://spincio-server.onrender.com/")); // the online list follows
    }

    /// <summary>
    /// The invitation bar and the online page both read the remembered server at start-up: the second must wait for
    /// the same reading, and a server from an invite link chosen meanwhile must not be overwritten by it.
    /// </summary>
    [Fact]
    public async Task The_remembered_server_is_read_once_and_an_invite_link_chosen_meanwhile_wins()
    {
        var remembered = JSInterop.Setup<string?>("localStorage.getItem", "spincio.server");
        var online = Services.GetRequiredService<OnlineService>();

        var first = online.LoadAsync();
        var second = online.LoadAsync();
        second.IsCompleted.ShouldBeFalse();
        (await online.UseServerAsync(Server)).ShouldBeTrue();
        remembered.SetResult("https://old-5080.app.github.dev/");
        await Task.WhenAll(first, second);

        online.ServerUrl.ShouldBe(new Uri(Server));
    }

    [Fact]
    public async Task The_player_profile_is_created_once_even_when_asked_twice_at_start_up()
    {
        var stored = JSInterop.Setup<string?>("localStorage.getItem", "spincio.player");
        var profiles = new ProfileService(JSInterop.JSRuntime);

        var first = profiles.LoadAsync();
        var second = profiles.LoadAsync();
        stored.SetResult(null);
        var (a, b) = (await first, await second);

        a.ShouldBeSameAs(b);
        a.Id.Length.ShouldBe(32);
        a.HasName.ShouldBeFalse();
        var saved = JSInterop.Invocations["localStorage.setItem"].ShouldHaveSingleItem();
        saved.Arguments[1]!.ToString().ShouldBe($$"""{"Id":"{{a.Id}}","Name":""}""");
    }

    /// <summary>ADR 0015: everyone else with the app open on the same server, each with an "Invita" button.</summary>
    [Fact]
    public void The_list_shows_who_else_is_online_with_an_invite_button()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.player").SetResult("""{"Id":"ada1","Name":"Ada"}""");
        var page = Render<Online>();

        _lobby.State = LobbyState.Online;
        _lobby.Players = [new("ada1", "Ada"), new("bea1", "Bea"), new("cleo1", "Cleo")];
        _lobby.SentTo["bea1"] = "Ha rifiutato";
        _lobby.Raise();

        _lobby.Starts.ShouldHaveSingleItem().Profile.ShouldBe(new PlayerProfile("ada1", "Ada"));
        page.FindAll(".online-players .player-name").Select(n => n.TextContent).ShouldBe(["Bea", "Cleo"]);
        page.Find(".online-players .invite-status").TextContent.ShouldBe("Ha rifiutato");
        page.FindAll(".online-players .invite-player").ShouldAllBe(b => !b.HasAttribute("disabled"));
        page.FindAll(".lobby-status").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(LobbyState.Connecting, "Collegamento al server… Se era spento ci vuole fino a un minuto.")]
    [InlineData(LobbyState.Unreachable, "Server non raggiungibile: riprovo tra poco.")]
    [InlineData(LobbyState.Online, "Per ora non c'è nessun altro. Qui compare chi apre l'app collegata a questo server.")]
    public void The_list_says_why_it_is_empty(LobbyState state, string status)
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.player").SetResult("""{"Id":"ada1","Name":"Ada"}""");
        var page = Render<Online>();

        _lobby.State = state;
        _lobby.Players = state == LobbyState.Online ? [new OnlinePlayer("ada1", "Ada")] : [];
        _lobby.Raise();

        page.Find(".lobby-status").TextContent.ShouldBe(status);
        page.FindAll(".online-players").ShouldBeEmpty();
    }

    [Fact]
    public void Writing_a_name_joins_the_online_list_and_remembers_the_name()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        var page = Render<Online>();
        page.Find(".lobby-status").TextContent.ShouldStartWith("Scrivi il tuo nome");
        _lobby.Starts.ShouldBeEmpty();

        page.Find("input[placeholder='Nome']").Input("Bea ");
        page.Find("input[placeholder='Nome']").Change("Bea ");

        var start = _lobby.Starts.ShouldHaveSingleItem();
        start.Server.ShouldBe(new Uri(Server));
        start.Profile.Name.ShouldBe("Bea");
        var saved = JSInterop.Invocations["localStorage.setItem"].Last(i => Equals(i.Arguments[0], "spincio.player"));
        saved.Arguments[1]!.ToString()!.ShouldContain("\"Name\":\"Bea\"");
    }

    [Fact]
    public void Forgetting_the_server_leaves_the_online_list()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        var page = Render<Online>();

        page.Find(".server-info button.link").Click();

        _lobby.Stops.ShouldBe(1);
        page.Find("input[inputmode='url']").ShouldNotBeNull();
    }

    /// <summary>SPEC §1.7: the mode of the room you create is chosen on the online page and remembered.</summary>
    [Fact]
    public void The_mode_of_the_room_is_chosen_on_the_online_page()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        var page = Render<Online>();
        page.Find(".mode-choice [aria-checked='true']").TextContent.ShouldBe("Spincio");

        page.FindAll(".mode-choice button").Single(b => b.TextContent == "Spincione").Click();

        page.Find(".mode-choice [aria-checked='true']").TextContent.ShouldBe("Spincione");
        Services.GetRequiredService<SettingsService>().Current.Variant.ShouldBe(Spincio.Engine.GameVariant.Spincione);
    }
}
