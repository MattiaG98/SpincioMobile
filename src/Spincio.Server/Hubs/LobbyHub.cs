using Microsoft.AspNetCore.SignalR;
using Spincio.Contracts;
using Spincio.Server.Lobby;

namespace Spincio.Server.Hubs;

/// <summary>Who is online and invitations (ADR 0015). Every call goes to the lobby registry.</summary>
public sealed class LobbyHub(LobbyRegistry lobby) : Hub<ILobbyClient>, ILobbyHub
{
    public Task<IReadOnlyList<OnlinePlayer>> Hello(string playerId, string name) => lobby.HelloAsync(Context.ConnectionId, playerId, name);

    public Task<CommandResult> Invite(string targetId, string seatToken) => lobby.InviteAsync(Context.ConnectionId, targetId, seatToken);

    public Task Answer(string invitationId, bool accepted) => lobby.AnswerAsync(Context.ConnectionId, invitationId, accepted);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await lobby.DisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
