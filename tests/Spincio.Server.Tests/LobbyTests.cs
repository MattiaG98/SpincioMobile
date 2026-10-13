using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Spincio.Contracts;
using Spincio.Server.Lobby;
using Spincio.Server.Rooms;

namespace Spincio.Server.Tests;

/// <summary>Records lobby messages per connection instead of sending them over SignalR.</summary>
internal sealed class RecordingLobbyNotifier : ILobbyNotifier
{
    public ConcurrentDictionary<string, IReadOnlyList<OnlinePlayer>> Lists { get; } = new();

    public ConcurrentDictionary<string, List<Invitation>> Invitations { get; } = new();

    public ConcurrentDictionary<string, List<(string Id, string ById, string By, bool Accepted)>> Answers { get; } = new();

    public Task PlayersChanged(string connectionId, IReadOnlyList<OnlinePlayer> players)
    {
        Lists[connectionId] = players;
        return Task.CompletedTask;
    }

    public Task Invited(string connectionId, Invitation invitation)
    {
        Invitations.GetOrAdd(connectionId, _ => []).Add(invitation);
        return Task.CompletedTask;
    }

    public Task InvitationAnswered(string connectionId, string invitationId, string byId, string byName, bool accepted)
    {
        Answers.GetOrAdd(connectionId, _ => []).Add((invitationId, byId, byName, accepted));
        return Task.CompletedTask;
    }
}

/// <summary>ADR 0015: who is online, invitations to a room, joining with or against the inviter.</summary>
public sealed class LobbyTests : IDisposable
{
    private readonly RoomRegistry _rooms = new(Options.Create(new RoomOptions()), new FakeTimeProvider(), new RecordingNotifier(), NullLoggerFactory.Instance);
    private readonly RecordingLobbyNotifier _notifier = new();
    private readonly LobbyRegistry _lobby;

    public LobbyTests()
    {
        _lobby = new LobbyRegistry(_rooms, _notifier);
    }

    public void Dispose() => _lobby.Dispose();

    private async Task<(GameRoom Room, JoinResult Host)> RoomOfAsync(string name, string connection)
    {
        var room = _rooms.Create();
        var host = await room.JoinAsync(name, connection);
        _rooms.Track(host, room, connection);
        return (room, host);
    }

    [Fact]
    public async Task Everyone_sees_who_is_online_and_who_leaves()
    {
        (await _lobby.HelloAsync("l-ada", "ada1", "Ada")).Select(p => p.Name).ShouldBe(["Ada"]);
        (await _lobby.HelloAsync("l-bea", "bea1", "  Bea  ")).Select(p => p.Name).ShouldBe(["Ada", "Bea"]);
        _notifier.Lists["l-ada"].Select(p => p.Name).ShouldBe(["Ada", "Bea"]);

        await _lobby.DisconnectedAsync("l-bea");

        _notifier.Lists["l-ada"].Select(p => p.Name).ShouldBe(["Ada"]);
    }

    [Fact]
    public async Task A_device_with_two_tabs_is_one_player_until_both_close()
    {
        await _lobby.HelloAsync("l-ada", "ada1", "Ada");
        await _lobby.HelloAsync("tab-1", "bea1", "Bea");
        await _lobby.HelloAsync("tab-2", "bea1", "Bea");
        _notifier.Lists["l-ada"].Count.ShouldBe(2);

        await _lobby.DisconnectedAsync("tab-1");
        _notifier.Lists["l-ada"].Count.ShouldBe(2);
        await _lobby.DisconnectedAsync("tab-2");
        _notifier.Lists["l-ada"].Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("", "Ada")]
    [InlineData("ada1", "   ")]
    [InlineData("ada-1!", "Ada")] // ids are letters and digits only
    public async Task Hello_needs_an_id_and_a_name(string id, string name)
    {
        (await _lobby.HelloAsync("l-x", id, name)).ShouldBeEmpty();
        _lobby.OnlineCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_invitation_reaches_every_connection_of_the_invited_player_and_the_answer_reaches_the_inviter()
    {
        await _lobby.HelloAsync("l-ada", "ada1", "Ada");
        await _lobby.HelloAsync("l-bea", "bea1", "Bea");
        var (room, host) = await RoomOfAsync("Ada", "g-ada");

        (await _lobby.InviteAsync("l-ada", "bea1", host.Token!)).Ok.ShouldBeTrue();

        var invitation = _notifier.Invitations["l-bea"].ShouldHaveSingleItem();
        invitation.ShouldBe(invitation with { FromId = "ada1", FromName = "Ada", RoomCode = room.Code, InviterSeat = 0, CanJoinWith = true, CanJoinAgainst = true });

        await _lobby.AnswerAsync("l-bea", invitation.Id, accepted: false);
        _notifier.Answers["l-ada"].ShouldBe([(invitation.Id, "bea1", "Bea", false)]);
    }

    [Fact]
    public async Task Invitations_that_cannot_work_are_refused()
    {
        await _lobby.HelloAsync("l-ada", "ada1", "Ada");
        var (_, host) = await RoomOfAsync("Ada", "g-ada");

        (await _lobby.InviteAsync("l-ada", "nobody", host.Token!)).Ok.ShouldBeFalse(); // not online
        (await _lobby.InviteAsync("l-ada", "ada1", host.Token!)).Ok.ShouldBeFalse(); // yourself
        await _lobby.HelloAsync("l-bea", "bea1", "Bea");
        (await _lobby.InviteAsync("l-ada", "bea1", "not-a-token")).Ok.ShouldBeFalse(); // no such room
        (await _lobby.InviteAsync("l-stranger", "bea1", host.Token!)).Ok.ShouldBeFalse(); // never said hello
    }

    [Fact]
    public async Task An_unanswered_invitation_comes_back_when_the_phone_reconnects()
    {
        await _lobby.HelloAsync("l-ada", "ada1", "Ada");
        await _lobby.HelloAsync("l-bea", "bea1", "Bea");
        var (_, host) = await RoomOfAsync("Ada", "g-ada");
        await _lobby.InviteAsync("l-ada", "bea1", host.Token!);
        await _lobby.DisconnectedAsync("l-bea"); // phone in the background

        await _lobby.HelloAsync("l-bea-again", "bea1", "Bea");

        _notifier.Invitations["l-bea-again"].ShouldHaveSingleItem().FromName.ShouldBe("Ada");
    }

    [Fact]
    public async Task Invited_players_join_with_or_against_the_inviter()
    {
        var (room, host) = await RoomOfAsync("Ada", "g-ada");

        (await room.JoinNearAsync("Bea", "g-bea", host.Seat, sameTeam: false)).Seat.ShouldBe(1);
        (await room.JoinNearAsync("Cleo", "g-cleo", host.Seat, sameTeam: true)).Seat.ShouldBe(2);
        (await room.JoinNearAsync("Dino", "g-dino", host.Seat, sameTeam: true)).Ok.ShouldBeFalse(); // Ada's team is full
        var snapshot = (await room.SnapshotForAsync(host.Token!)).ShouldNotBeNull();
        snapshot.CanJoinWith.ShouldBeFalse();
        snapshot.CanJoinAgainst.ShouldBeTrue();
        (await room.JoinNearAsync("Dino", "g-dino", host.Seat, sameTeam: false)).Seat.ShouldBe(3);
    }
}
