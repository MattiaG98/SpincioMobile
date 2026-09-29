using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;
using Spincio.Server.Rooms;

namespace Spincio.Server.Tests;

/// <summary>Records what each connection received, instead of sending it over SignalR.</summary>
internal sealed class RecordingNotifier : IRoomNotifier
{
    public ConcurrentDictionary<string, List<GameUpdate>> Updates { get; } = new();

    public ConcurrentDictionary<string, RoomInfo> Rooms { get; } = new();

    public ConcurrentDictionary<string, MatchReveal> Reveals { get; } = new();

    public GameUpdate Last(string connection) => Updates[connection][^1];

    public Task RoomChanged(string connectionId, RoomInfo room)
    {
        Rooms[connectionId] = room;
        return Task.CompletedTask;
    }

    public Task Updated(string connectionId, GameUpdate update)
    {
        Updates.GetOrAdd(connectionId, _ => []).Add(update);
        return Task.CompletedTask;
    }

    public Task Revealed(string connectionId, MatchReveal reveal)
    {
        Reveals[connectionId] = reveal;
        return Task.CompletedTask;
    }
}

/// <summary>A room with fake time and a recording notifier; time only moves when a test advances it.</summary>
internal sealed class RoomHarness : IAsyncDisposable
{
    public RoomHarness(ulong seed = 42, BotLevel botLevel = BotLevel.Greedy)
    {
        Options = new RoomOptions { BotLevel = botLevel };
        Room = new GameRoom("TEST1", Options, Time, Notifier, NullLogger.Instance, () => seed);
    }

    public FakeTimeProvider Time { get; } = new();

    public RecordingNotifier Notifier { get; } = new();

    public RoomOptions Options { get; }

    public GameRoom Room { get; }

    public MatchState State => Room.State!;

    public async Task<JoinResult> JoinAsync(string name, string connection)
    {
        var result = await Room.JoinAsync(name, connection);
        result.Ok.ShouldBeTrue(result.Error);
        return result;
    }

    /// <summary>Advances fake time in small steps, letting queued work run after each step.</summary>
    public async Task AdvanceAsync(TimeSpan total)
    {
        var step = TimeSpan.FromMilliseconds(100);
        for (var elapsed = TimeSpan.Zero; elapsed < total; elapsed += step)
        {
            Time.Advance(step);
            await Room.WhenIdleAsync();
        }
    }

    /// <summary>Lets CPUs play until it is <paramref name="seat"/>'s turn or the match ends.</summary>
    public async Task UntilTurnOfAsync(int seat)
    {
        for (int i = 0; i < 400 && State.Phase == MatchPhase.AwaitingPlay && State.ToPlay.Index != seat; i++)
        {
            await AdvanceAsync(Options.BotDelay);
        }
    }

    public ValueTask DisposeAsync() => Room.DisposeAsync();
}
