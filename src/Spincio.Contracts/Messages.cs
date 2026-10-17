using Spincio.Engine;

namespace Spincio.Contracts;

public static class HubPaths
{
    public const string Game = "/hubs/game";

    /// <summary>Who is online and invitations (ADR 0015).</summary>
    public const string Lobby = "/hubs/lobby";
}

/// <summary>Result of creating, joining or rejoining a room. <see cref="Token"/> identifies the player's seat.</summary>
public sealed record JoinResult(bool Ok, string? Error, string? RoomCode, int Seat, string? Token)
{
    public static JoinResult Fail(string error) => new(false, error, null, -1, null);
}

public sealed record CommandResult(bool Ok, string? Error)
{
    public static CommandResult Success { get; } = new(true, null);

    public static CommandResult Fail(string error) => new(false, error);
}

public sealed record SeatInfo(int Seat, string Name, bool IsBot, bool Connected);

/// <summary>
/// Room state visible to everyone. <see cref="Commitment"/> is SHA-256(seed ‖ salt), published when the match
/// starts; the seed and salt are revealed at the end so anyone can check the deal was not rigged (ADR 0004).
/// </summary>
public sealed record RoomInfo(string Code, IReadOnlyList<SeatInfo> Seats, bool Started, string? Commitment);

/// <summary>
/// What one seat receives after every accepted command: its own view and the events addressed to it.
/// <see cref="Sequence"/> is the value to send back as <c>expectedSequence</c>.
/// </summary>
public sealed record GameUpdate(int Sequence, PlayerView View, IReadOnlyList<GameEvent> Events, int TurnSecondsLeft)
{
    /// <summary>
    /// Sealed restore point of the whole match (ADR 0017): the client keeps it and hands it back with
    /// <see cref="IGameHub.Resume"/> if the server restarted. Only the server can read it.
    /// </summary>
    public string? Checkpoint { get; init; }
}

/// <summary>End of match: everything needed to verify the commitment and replay the whole match.</summary>
public sealed record MatchReveal(ulong Seed, string Salt, IReadOnlyList<string> Commands)
{
    /// <summary>The game mode the match was played in (SPEC §1.7), needed to replay it.</summary>
    public GameVariant Variant { get; init; }
}

/// <summary>Server → client calls.</summary>
public interface IGameClient
{
    Task RoomChanged(RoomInfo room);

    Task Updated(GameUpdate update);

    Task Revealed(MatchReveal reveal);
}

/// <summary>Client → server calls (method names of the SignalR hub).</summary>
public interface IGameHub
{
    Task<JoinResult> CreateRoom(string playerName);

    Task<JoinResult> JoinRoom(string roomCode, string playerName);

    /// <summary>
    /// Joins next to a player who sent an invitation: on the same team as <paramref name="inviterSeat"/>
    /// (<paramref name="sameTeam"/>) or on the other one. Fails if that team has no free seat.
    /// </summary>
    Task<JoinResult> JoinRoomAs(string roomCode, string playerName, int inviterSeat, bool sameTeam);

    /// <summary>Reconnects a seat after a disconnection; the server answers with a fresh snapshot.</summary>
    Task<JoinResult> Rejoin(string token);

    /// <summary>
    /// Like <see cref="Rejoin"/>, but if the server no longer knows the match (it restarted), it rebuilds it from
    /// <paramref name="checkpoint"/>, the last <see cref="GameUpdate.Checkpoint"/> this player received (ADR 0017).
    /// </summary>
    Task<JoinResult> Resume(string token, string checkpoint);

    /// <summary>Starts the match; empty seats are taken by CPU players.</summary>
    Task<CommandResult> Start(string token);

    /// <summary>Plays a command encoded with <see cref="CommandCodec"/>. Rejected if the sequence is stale.</summary>
    Task<CommandResult> Play(string token, int expectedSequence, string command);
}

/// <summary>Someone with the app open and connected to the server (ADR 0015). <see cref="Id"/> identifies the device.</summary>
public sealed record OnlinePlayer(string Id, string Name);

/// <summary>
/// An invitation to a room. The invited player chooses to play with the inviter (<see cref="CanJoinWith"/>: a free seat
/// on the inviter's team) or against (<see cref="CanJoinAgainst"/>).
/// </summary>
public sealed record Invitation(
    string Id, string FromId, string FromName, string RoomCode, int InviterSeat, bool CanJoinWith, bool CanJoinAgainst);

/// <summary>Lobby, server → client calls.</summary>
public interface ILobbyClient
{
    Task PlayersChanged(IReadOnlyList<OnlinePlayer> players);

    Task Invited(Invitation invitation);

    /// <summary>To the inviter: the invited player accepted (and joined the room) or declined.</summary>
    Task InvitationAnswered(string invitationId, string byId, string byName, bool accepted);
}

/// <summary>Lobby, client → server calls (ADR 0015).</summary>
public interface ILobbyHub
{
    /// <summary>Announces this device (its random id and the player's name); returns who is online.</summary>
    Task<IReadOnlyList<OnlinePlayer>> Hello(string playerId, string name);

    /// <summary>Invites an online player to the room of the seat identified by <paramref name="seatToken"/>.</summary>
    Task<CommandResult> Invite(string targetId, string seatToken);

    /// <summary>The invited player's answer; when accepting, call it after joining the room.</summary>
    Task Answer(string invitationId, bool accepted);
}
