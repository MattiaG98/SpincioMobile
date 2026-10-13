using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Spincio.Server.Rooms;

namespace Spincio.Server.Tests;

/// <summary>ADR 0014: the server writes a heartbeat only while someone is connected.</summary>
public class ActivityHeartbeatTests
{
    [Fact]
    public async Task Beats_only_while_someone_is_connected()
    {
        var registry = new RoomRegistry(Options.Create(new RoomOptions()), new FakeTimeProvider(), new RecordingNotifier(), NullLoggerFactory.Instance);
        var heartbeat = new ActivityHeartbeat(registry, new FakeTimeProvider(), NullLogger<ActivityHeartbeat>.Instance);

        heartbeat.Beat().ShouldBeFalse();

        var room = registry.Create();
        var ada = await room.JoinAsync("Ada", "c-ada");
        registry.Track(ada, room, "c-ada");
        registry.Activity().ShouldBe((1, 1));
        heartbeat.Beat().ShouldBeTrue();

        await registry.DisconnectedAsync("c-ada");
        heartbeat.Beat().ShouldBeFalse();
    }
}
