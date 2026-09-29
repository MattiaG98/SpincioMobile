using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Spincio.Contracts;

namespace Spincio.Server.Rooms;

/// <summary>All rooms of this server instance, in memory (ADR 0010: no database for the first online version).</summary>
public sealed class RoomRegistry(IOptions<RoomOptions> options, TimeProvider time, IRoomNotifier notifier, ILoggerFactory loggers)
{
    /// <summary>Room codes avoid look-alike characters (0/O, 1/I).</summary>
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;

    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, GameRoom> _byToken = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GameRoom> _byConnection = new(StringComparer.Ordinal);

    /// <summary>Overrides the random match seeds (tests only).</summary>
    public Func<ulong>? SeedSource { get; set; }

    public int Count => _rooms.Count;

    public GameRoom Create()
    {
        Purge();
        while (true)
        {
            string code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            var room = new GameRoom(code, options.Value, time, notifier, loggers.CreateLogger<GameRoom>(), SeedSource);
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
