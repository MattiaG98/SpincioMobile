using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Spincio.Contracts;
using Spincio.Server.Hubs;
using Spincio.Server.Rooms;

namespace Spincio.Server.Lobby;

/// <summary>Delivers lobby messages to connections. Abstracted so the lobby can be tested without SignalR.</summary>
public interface ILobbyNotifier
{
    Task PlayersChanged(string connectionId, IReadOnlyList<OnlinePlayer> players);

    Task Invited(string connectionId, Invitation invitation);

    Task InvitationAnswered(string connectionId, string invitationId, string byId, string byName, bool accepted);
}

public sealed class HubLobbyNotifier(IHubContext<LobbyHub, ILobbyClient> hub) : ILobbyNotifier
{
    public Task PlayersChanged(string connectionId, IReadOnlyList<OnlinePlayer> players) =>
        hub.Clients.Client(connectionId).PlayersChanged(players);

    public Task Invited(string connectionId, Invitation invitation) => hub.Clients.Client(connectionId).Invited(invitation);

    public Task InvitationAnswered(string connectionId, string invitationId, string byId, string byName, bool accepted) =>
        hub.Clients.Client(connectionId).InvitationAnswered(invitationId, byId, byName, accepted);
}

/// <summary>
/// Who has the app open and connected, and the invitations not yet answered (ADR 0015). In memory, like the rooms:
/// a player is identified by a random id their device keeps; a device may have several connections (tabs).
/// An invitation waits until it is answered, so a phone that was in the background gets it when it comes back.
/// </summary>
public sealed class LobbyRegistry(RoomRegistry rooms, ILobbyNotifier notifier) : IDisposable
{
    public const int MaxNameLength = 16;

    private readonly Lock _gate = new();

    // Player lists are sent one change at a time, so nobody receives an older list after a newer one.
    private readonly SemaphoreSlim _lists = new(1, 1);
    private readonly Dictionary<string, Player> _players = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pending> _invitations = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<OnlinePlayer>> HelloAsync(string connectionId, string playerId, string name)
    {
        string? id = CleanId(playerId);
        string clean = CleanName(name);
        if (id is null || clean.Length == 0)
        {
            return [];
        }

        IReadOnlyList<OnlinePlayer> list;
        List<Invitation> pending;
        await _lists.WaitAsync();
        try
        {
            List<string> everyone;
            lock (_gate)
            {
                if (_byConnection.TryGetValue(connectionId, out var previous) && previous != id)
                {
                    Remove(connectionId);
                }

                if (!_players.TryGetValue(id, out var player))
                {
                    _players[id] = player = new Player();
                }

                player.Name = clean;
                player.Connections.Add(connectionId);
                _byConnection[connectionId] = id;
                list = Snapshot();
                everyone = [.. _byConnection.Keys];
                pending = [.. _invitations.Values.Where(p => p.TargetId == id).Select(p => p.Invitation)];
            }

            await BroadcastAsync(everyone, list);
        }
        finally
        {
            _lists.Release();
        }

        foreach (var invitation in pending)
        {
            await notifier.Invited(connectionId, invitation);
        }

        return list;
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        await _lists.WaitAsync();
        try
        {
            IReadOnlyList<OnlinePlayer> list;
            List<string> everyone;
            lock (_gate)
            {
                if (!Remove(connectionId))
                {
                    return;
                }

                list = Snapshot();
                everyone = [.. _byConnection.Keys];
            }

            await BroadcastAsync(everyone, list);
        }
        finally
        {
            _lists.Release();
        }
    }

    public async Task<CommandResult> InviteAsync(string connectionId, string targetId, string seatToken)
    {
        string fromId;
        lock (_gate)
        {
            if (!_byConnection.TryGetValue(connectionId, out fromId!))
            {
                return CommandResult.Fail("Collegati alla lista dei giocatori, poi riprova.");
            }

            if (!_players.ContainsKey(targetId ?? ""))
            {
                return CommandResult.Fail("Il giocatore non è più online.");
            }

            if (targetId == fromId)
            {
                return CommandResult.Fail("Non puoi invitare te stesso.");
            }
        }

        if (rooms.FindByToken(seatToken) is not { } room || await room.SnapshotForAsync(seatToken) is not { } seat)
        {
            return CommandResult.Fail("La stanza non esiste più o la partita è già iniziata.");
        }

        if (!seat.CanJoinWith && !seat.CanJoinAgainst)
        {
            return CommandResult.Fail("La stanza è piena.");
        }

        List<string> targets;
        Invitation invitation;
        lock (_gate)
        {
            if (!_players.TryGetValue(targetId!, out var target) || !_players.TryGetValue(fromId, out var from))
            {
                return CommandResult.Fail("Il giocatore non è più online.");
            }

            // One invitation per inviter and invited player: a new one replaces the old.
            foreach (var old in _invitations.Where(e => e.Value.TargetId == targetId && e.Value.Invitation.FromId == fromId).ToList())
            {
                _invitations.Remove(old.Key);
            }

            invitation = new Invitation(
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), fromId, from.Name, room.Code, seat.Seat,
                seat.CanJoinWith, seat.CanJoinAgainst);
            _invitations[invitation.Id] = new Pending(invitation, targetId!);
            targets = [.. target.Connections];
        }

        foreach (var connection in targets)
        {
            await notifier.Invited(connection, invitation);
        }

        return CommandResult.Success;
    }

    public async Task AnswerAsync(string connectionId, string invitationId, bool accepted)
    {
        List<string> inviters;
        string byName;
        string byId;
        lock (_gate)
        {
            if (!_byConnection.TryGetValue(connectionId, out var playerId)
                || !_invitations.TryGetValue(invitationId ?? "", out var pending)
                || pending.TargetId != playerId)
            {
                return;
            }

            _invitations.Remove(invitationId!);
            byId = playerId;
            byName = _players[playerId].Name;
            inviters = _players.TryGetValue(pending.Invitation.FromId, out var from) ? [.. from.Connections] : [];
        }

        foreach (var connection in inviters)
        {
            await notifier.InvitationAnswered(connection, invitationId!, byId, byName, accepted);
        }
    }

    /// <summary>People online right now.</summary>
    public int OnlineCount
    {
        get
        {
            lock (_gate)
            {
                return _players.Count;
            }
        }
    }

    public void Dispose() => _lists.Dispose();

    public static string CleanName(string? name)
    {
        var letters = (name ?? "").Where(c => !char.IsControl(c)).ToArray();
        var clean = new string(letters).Trim();
        return clean.Length > MaxNameLength ? clean[..MaxNameLength].Trim() : clean;
    }

    private static string? CleanId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64 && id.All(char.IsAsciiLetterOrDigit) ? id : null;

    private bool Remove(string connectionId)
    {
        if (!_byConnection.Remove(connectionId, out var id))
        {
            return false;
        }

        if (_players.TryGetValue(id, out var player) && player.Connections.Remove(connectionId) && player.Connections.Count == 0)
        {
            _players.Remove(id);
        }

        return true;
    }

    private OnlinePlayer[] Snapshot() =>
        [.. _players.Select(p => new OnlinePlayer(p.Key, p.Value.Name)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];

    private async Task BroadcastAsync(List<string> connections, IReadOnlyList<OnlinePlayer> list)
    {
        foreach (var connection in connections)
        {
            await notifier.PlayersChanged(connection, list);
        }
    }

    private sealed class Player
    {
        public string Name { get; set; } = "";

        public HashSet<string> Connections { get; } = new(StringComparer.Ordinal);
    }

    private sealed record Pending(Invitation Invitation, string TargetId);
}
