using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Server.Rooms;

/// <summary>All rooms of this server instance, in memory (ADR 0010: no database for the first online version).</summary>
public sealed class RoomRegistry(
    IOptions<RoomOptions> options,
    TimeProvider time,
    IRoomNotifier notifier,
    ILoggerFactory loggers,
    CheckpointProtector? checkpoints = null)
{
    /// <summary>Room codes avoid look-alike characters (0/O, 1/I).</summary>
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;

    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, GameRoom> _byToken = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GameRoom> _byConnection = new(StringComparer.Ordinal);
    private readonly CheckpointProtector _checkpoints = checkpoints ?? new CheckpointProtector(options.Value.CheckpointKey);

    /// <summary>Overrides the random match seeds (tests only).</summary>
    public Func<ulong>? SeedSource { get; set; }

    public int Count => _rooms.Count;

    /// <summary>Rooms with someone connected, and how many people are connected.</summary>
    public (int Rooms, int Players) Activity() => (_byConnection.Values.Distinct().Count(), _byConnection.Count);

    public GameRoom Create(GameVariant variant = GameVariant.Spincio)
    {
        Purge();
        while (true)
        {
            string code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            var room = NewRoom(code, variant);
            if (_rooms.TryAdd(code, room))
            {
                return room;
            }
        }
    }

    public GameRoom? Find(string code) =>
        _rooms.TryGetValue((code ?? "").Trim(), out var room) ? room : null;

    public GameRoom? FindByToken(string token) =>
        _byToken.TryGetValue(token ?? "", out var room) ? room : null;

    /// <summary>
    /// The room of a match this server no longer knows (it restarted), rebuilt from a player's restore point
    /// (ADR 0017); null if the checkpoint is not valid or <paramref name="token"/> is not one of its players. If
    /// another player of the same match already brought it back, that room.
    /// </summary>
    public async Task<GameRoom?> RestoreAsync(string token, string checkpointText)
    {
        if (_checkpoints.Unprotect(checkpointText) is not { } checkpoint
            || string.IsNullOrEmpty(token)
            || !checkpoint.Seats.Any(s => s.Token == token))
        {
            return null;
        }

        var room = NewRoom(checkpoint.Code, checkpoint.Variant);
        var restoring = room.RestoreAsync(checkpoint); // queued before anyone can reach the room
        if (!_rooms.TryAdd(checkpoint.Code, room))
        {
            await room.DisposeAsync();
            return Find(checkpoint.Code);
        }

        if (!await restoring)
        {
            _rooms.TryRemove(checkpoint.Code, out _);
            await room.DisposeAsync();
            return null;
        }

        foreach (var seat in checkpoint.Seats.Where(s => s.Token is not null))
        {
            _byToken[seat.Token!] = room;
        }

        return room;
    }

    /// <summary>Brings <paramref name="room"/> forward if <paramref name="checkpointText"/> knows more moves of its match.</summary>
    public Task<bool> ExtendAsync(GameRoom room, string checkpointText)
    {
        ArgumentNullException.ThrowIfNull(room);
        return _checkpoints.Unprotect(checkpointText) is { } checkpoint && checkpoint.Code == room.Code
            ? room.ExtendAsync(checkpoint)
            : Task.FromResult(false);
    }

    /// <summary>Remembers which room a successful join belongs to, by token and by connection.</summary>
    public void Track(JoinResult result, GameRoom room, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is { Ok: true, Token: { } token })
        {
            _byToken[token] = room;
            _byConnection[connectionId] = room;
        }
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        if (_byConnection.TryRemove(connectionId, out var room))
        {
            await room.DisconnectedAsync(connectionId);
        }
    }

    private GameRoom NewRoom(string code, GameVariant variant) =>
        new(code, options.Value, time, notifier, loggers.CreateLogger<GameRoom>(), SeedSource, _checkpoints, variant);

    private void Purge()
    {
        var limit = time.GetUtcNow() - options.Value.IdleLifetime;
        foreach (var (code, room) in _rooms)
        {
            if (room.LastActivity < limit && _rooms.TryRemove(code, out _))
            {
                foreach (var entry in _byToken.Where(e => e.Value == room).ToList())
                {
                    _byToken.TryRemove(entry.Key, out _);
                }

                foreach (var entry in _byConnection.Where(e => e.Value == room).ToList())
                {
                    _byConnection.TryRemove(entry.Key, out _);
                }

                _ = room.DisposeAsync().AsTask();
            }
        }
    }
}
