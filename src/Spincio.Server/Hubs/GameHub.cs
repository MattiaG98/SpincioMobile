using Microsoft.AspNetCore.SignalR;
using Spincio.Contracts;
using Spincio.Server.Rooms;

namespace Spincio.Server.Hubs;

/// <summary>SignalR entry point. Stateless: every call is routed to the room that owns the token or code.</summary>
public sealed class GameHub(RoomRegistry rooms) : Hub<IGameClient>, IGameHub
{
    public async Task<JoinResult> CreateRoom(string playerName)
    {
        var room = rooms.Create();
        var result = await room.JoinAsync(playerName, Context.ConnectionId);
        rooms.Track(result, room, Context.ConnectionId);
        return result;
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string playerName)
    {
        if (rooms.Find(roomCode) is not { } room)
        {
            return JoinResult.Fail("Stanza non trovata.");
        }

        var result = await room.JoinAsync(playerName, Context.ConnectionId);
        rooms.Track(result, room, Context.ConnectionId);
        return result;
    }

    public async Task<JoinResult> Rejoin(string token)
    {
        if (rooms.FindByToken(token) is not { } room)
        {
            return JoinResult.Fail("Partita non trovata.");
        }

        var result = await room.RejoinAsync(token, Context.ConnectionId);
        rooms.Track(result, room, Context.ConnectionId);
        return result;
    }

    public Task<CommandResult> Start(string token) =>
        rooms.FindByToken(token) is { } room ? room.StartAsync(token) : Task.FromResult(CommandResult.Fail("Partita non trovata."));

    public Task<CommandResult> Play(string token, int expectedSequence, string command) =>
        rooms.FindByToken(token) is { } room
            ? room.PlayAsync(token, expectedSequence, command)
            : Task.FromResult(CommandResult.Fail("Partita non trovata."));

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await rooms.DisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
