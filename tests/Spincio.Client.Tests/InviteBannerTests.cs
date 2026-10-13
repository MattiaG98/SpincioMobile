using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spincio.Client.Components;
using Spincio.Client.Game;
using Spincio.Contracts;

namespace Spincio.Client.Tests;

/// <summary>An <see cref="ILobby"/> driven by the test: what the server would have sent, and what the page asked.</summary>
public sealed class FakeLobby : ILobby
{
    public event Action? Changed;

    public LobbyState State { get; set; }

    public string? MyId { get; set; }

    public IReadOnlyList<OnlinePlayer> Players { get; set; } = [];

    public Invitation? Pending { get; set; }

    public Dictionary<string, string> SentTo { get; } = [];

    public IReadOnlyDictionary<string, string> Sent => SentTo;

    public List<(Uri Server, PlayerProfile Profile)> Starts { get; } = [];

    public List<(Invitation Invitation, bool Accepted)> Answers { get; } = [];

    public int Stops { get; private set; }

    public Task StartAsync(Uri server, PlayerProfile profile)
    {
        Starts.Add((server, profile));
        MyId = profile.Id;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stops++;
        return Task.CompletedTask;
    }

    public Task<CommandResult> InviteAsync(OnlinePlayer player, string seatToken) => Task.FromResult(CommandResult.Success);

    public Task AnswerAsync(Invitation invitation, bool accepted)
    {
        Answers.Add((invitation, accepted));
        Pending = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public void Raise() => Changed?.Invoke();
}

/// <summary>ADR 0015: an invitation shows at the top of any screen; accepting asks whether to play with or against.</summary>
public class InviteBannerTests : BunitContext
{
    private const string Server = "https://spincio-abc-5080.app.github.dev/";
    private readonly FakeLobby _lobby = new();

    public InviteBannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<IOnlineSeatStore, InMemoryOnlineSeatStore>();
        Services.AddSingleton<OnlineService>();
        Services.AddSingleton<ProfileService>();
        Services.AddSingleton<ILobby>(_lobby);
    }

    private static Invitation From(string name, bool with = true, bool against = true) =>
        new("inv1", "ada1", name, "K7QPZ", 0, with, against);

    [Fact]
    public void Without_an_invitation_nothing_shows()
    {
        var banner = Render<InviteBanner>();

        banner.FindAll(".invite-banner").ShouldBeEmpty();
    }

    [Fact]
    public void An_invitation_shows_who_invites_with_accept_and_decline()
    {
        var banner = Render<InviteBanner>();

        _lobby.Pending = From("Ada");
        _lobby.Raise();

        banner.Find(".invite-banner .invite-text").TextContent.ShouldContain("Ada ti invita");
        banner.FindAll(".invite-actions button").Select(b => b.TextContent.Trim()).ShouldBe(["Accetta", "Rifiuta"]);
    }

    [Fact]
    public void Declining_answers_no_and_the_invitation_goes_away()
    {
        _lobby.Pending = From("Ada");
        var banner = Render<InviteBanner>();

        banner.Find(".invite-decline").Click();

        _lobby.Answers.ShouldHaveSingleItem().ShouldBe((From("Ada"), false));
        banner.FindAll(".invite-banner").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, true, new[] { "Con Ada", "Contro Ada", "Rifiuta" })]
    [InlineData(false, true, new[] { "Contro Ada", "Rifiuta" })] // Ada's partner seat is taken
    [InlineData(true, false, new[] { "Con Ada", "Rifiuta" })] // the other team is full
    public void Accepting_offers_to_play_with_or_against_the_inviter_where_there_is_room(bool with, bool against, string[] buttons)
    {
        _lobby.Pending = From("Ada", with, against);
        var banner = Render<InviteBanner>();

        banner.Find(".invite-accept").Click();

        banner.Find(".invite-hint").TextContent.ShouldBe("Come vuoi giocare?");
        banner.FindAll(".invite-actions button").Select(b => b.TextContent.Trim()).ShouldBe(buttons);
        _lobby.Answers.ShouldBeEmpty(); // the answer goes only once the seat is taken
    }

    [Fact]
    public void With_a_server_and_a_name_the_app_joins_the_online_list_from_any_screen()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.player").SetResult("""{"Id":"bea1","Name":"Bea"}""");

        Render<InviteBanner>();

        _lobby.Starts.ShouldHaveSingleItem().ShouldBe((new Uri(Server), new PlayerProfile("bea1", "Bea")));
    }

    [Fact]
    public void Without_a_name_the_app_stays_out_of_the_online_list()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.server").SetResult(Server);

        Render<InviteBanner>();

        _lobby.Starts.ShouldBeEmpty();
    }
}
