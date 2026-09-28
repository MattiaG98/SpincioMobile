using Microsoft.AspNetCore.SignalR;
using Spincio.Contracts;
using Spincio.Server.Hubs;

namespace Spincio.Server.Rooms;

/// <summary>Delivers room messages to connections. Abstracted so rooms can be tested without SignalR.</summary>
public interface IRoomNotifier
{
    Task RoomChanged(string connectionId, RoomInfo room);

    Task Updated(string connectionId, GameUpdate update);

    Task Revealed(string connectionId, MatchReveal reveal);
}

public sealed class HubRoomNotifier(IHubContext<GameHub, IGameClient> hub) : IRoomNotifier
{
    public Task RoomChanged(string connectionId, RoomInfo room) => hub.Clients.Client(connectionId).RoomChanged(room);

    public Task Updated(string connectionId, GameUpdate update) => hub.Clients.Client(connectionId).Updated(update);

    public Task Revealed(string connectionId, MatchReveal reveal) => hub.Clients.Client(connectionId).Revealed(reveal);
}
