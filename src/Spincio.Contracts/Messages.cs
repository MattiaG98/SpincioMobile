using Spincio.Engine;

namespace Spincio.Contracts;

public static class HubPaths
{
    public const string Game = "/hubs/game";
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
public sealed record GameUpdate(int Sequence, PlayerView View, IReadOnlyList<GameEvent> Events, int TurnSecondsLeft);

/// <summary>End of match: everything needed to verify the commitment and replay the whole match.</summary>
public sealed record MatchReveal(ulong Seed, string Salt, IReadOnlyList<string> Commands);

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

    /// <summary>Reconnects a seat after a disconnection; the server answers with a fresh snapshot.</summary>
    Task<JoinResult> Rejoin(string token);

    /// <summary>Starts the match; empty seats are taken by CPU players.</summary>
    Task<CommandResult> Start(string token);

    /// <summary>Plays a command encoded with <see cref="CommandCodec"/>. Rejected if the sequence is stale.</summary>
    Task<CommandResult> Play(string token, int expectedSequence, string command);
}
