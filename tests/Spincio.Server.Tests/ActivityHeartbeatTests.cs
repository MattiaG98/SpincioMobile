using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Spincio.Server.Lobby;
using Spincio.Server.Rooms;

namespace Spincio.Server.Tests;

/// <summary>ADR 0014: the server writes a heartbeat only while someone is connected.</summary>
public class ActivityHeartbeatTests
{
    [Fact]
    public async Task Beats_only_while_someone_is_connected()
    {
        var registry = new RoomRegistry(Options.Create(new RoomOptions()), new FakeTimeProvider(), new RecordingNotifier(), NullLoggerFactory.Instance);
        using var lobby = new LobbyRegistry(registry, new RecordingLobbyNotifier());
        var heartbeat = new ActivityHeartbeat(registry, lobby, new FakeTimeProvider(), NullLogger<ActivityHeartbeat>.Instance);

        heartbeat.Beat().ShouldBeFalse();

        var room = registry.Create();
        var ada = await room.JoinAsync("Ada", "c-ada");
        registry.Track(ada, room, "c-ada");
        registry.Activity().ShouldBe((1, 1));
        heartbeat.Beat().ShouldBeTrue();

        await registry.DisconnectedAsync("c-ada");
        heartbeat.Beat().ShouldBeFalse();

        await lobby.HelloAsync("l-bea", "bea1", "Bea"); // the app open on the online page also counts
        heartbeat.Beat().ShouldBeTrue();
    }
}
