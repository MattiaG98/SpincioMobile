using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;
using Spincio.Server.Rooms;

namespace Spincio.Server.Tests;

/// <summary>ADR 0017: a started match survives a server restart through the restore point its players keep.</summary>
public class CheckpointTests
{
    private const string Key = "test-checkpoint-key";

    /// <summary>One server instance: its rooms, fake time and what it sent. A "restart" is a new instance.</summary>
    private sealed class Instance
    {
        public Instance(string? key = Key)
        {
            Options = new RoomOptions { BotLevel = BotLevel.Greedy, CheckpointKey = key };
            Rooms = new RoomRegistry(Microsoft.Extensions.Options.Options.Create(Options), Time, Notifier, NullLoggerFactory.Instance)
            {
                SeedSource = () => 7,
            };
        }

        public FakeTimeProvider Time { get; } = new();

        public RecordingNotifier Notifier { get; } = new();

        public RoomOptions Options { get; }

        public RoomRegistry Rooms { get; }

        public async Task AdvanceAsync(GameRoom room, TimeSpan total)
        {
            var step = TimeSpan.FromMilliseconds(100);
            for (var elapsed = TimeSpan.Zero; elapsed < total; elapsed += step)
            {
                Time.Advance(step);
                await room.WhenIdleAsync();
            }
        }

        /// <summary>Nobody moves: turn timeouts make a CPU play for the people.</summary>
        public async Task PlayMovesAsync(GameRoom room, int moves)
        {
            int target = room.State!.Sequence + moves;
            for (int i = 0; i < 400 && room.State.Sequence < target && room.State.Phase == MatchPhase.AwaitingPlay; i++)
            {
                await AdvanceAsync(room, Options.TurnTimeout);
            }
        }
    }

    private static string Json(PlayerView view) => JsonSerializer.Serialize(view, SpincioJson.Options);

    private static async Task<(GameRoom Room, JoinResult Ada, JoinResult Bea)> StartedMatchAsync(Instance server)
    {
        var room = server.Rooms.Create();
        var ada = await room.JoinAsync("Ada", "c-ada");
        server.Rooms.Track(ada, room, "c-ada");
        var bea = await room.JoinAsync("Bea", "c-bea");
        server.Rooms.Track(bea, room, "c-bea");
        (await room.StartAsync(ada.Token!)).Ok.ShouldBeTrue();
        return (room, ada, bea);
    }

    [Fact]
    public async Task A_started_match_comes_back_after_a_restart_and_is_still_the_same_verifiable_match()
    {
        var before = new Instance();
        var (room, ada, bea) = await StartedMatchAsync(before);
        await before.PlayMovesAsync(room, 12);
        var last = before.Notifier.Last("c-ada");
        string commitment = before.Notifier.Rooms["c-ada"].Commitment!;
        last.Checkpoint.ShouldNotBeNullOrEmpty();

        var after = new Instance(); // restarted: same key, no rooms
        var restored = (await after.Rooms.RestoreAsync(ada.Token!, last.Checkpoint!)).ShouldNotBeNull();
        var back = await restored.RejoinAsync(ada.Token!, "c-ada-2");

        back.Seat.ShouldBe(ada.Seat);
        restored.Code.ShouldBe(room.Code);
        var update = after.Notifier.Last("c-ada-2");
        update.Sequence.ShouldBe(last.Sequence);
        Json(update.View).ShouldBe(Json(last.View));
        after.Notifier.Rooms["c-ada-2"].Commitment.ShouldBe(commitment);
        after.Rooms.FindByToken(bea.Token!).ShouldBeSameAs(restored);
        (await after.Rooms.RestoreAsync(bea.Token!, last.Checkpoint!)).ShouldBeSameAs(restored); // Bea comes back to the same room

        // The rebuilt match goes on to the end and still matches the commitment published at the start.
        for (int i = 0; i < 2000 && restored.State!.Phase == MatchPhase.AwaitingPlay; i++)
        {
            await after.AdvanceAsync(restored, after.Options.TurnTimeout);
        }

        restored.State!.Phase.ShouldBe(MatchPhase.MatchOver);
        Commitment.Verify(commitment, after.Notifier.Reveals["c-ada-2"], restored.State.Score).ShouldBeTrue();
        after.Notifier.Last("c-ada-2").Checkpoint!.Length.ShouldBeLessThan(16_000); // fits a SignalR message (32 KB)
    }

    [Fact]
    public async Task A_changed_foreign_or_unknown_restore_point_is_refused()
    {
        var before = new Instance();
        var (room, ada, _) = await StartedMatchAsync(before);
        await before.PlayMovesAsync(room, 4);
        string checkpoint = before.Notifier.Last("c-ada").Checkpoint!;
        char[] changed = checkpoint.ToCharArray();
        changed[checkpoint.Length / 2] = changed[checkpoint.Length / 2] == 'A' ? 'B' : 'A';

        (await new Instance(key: "another-key").Rooms.RestoreAsync(ada.Token!, checkpoint)).ShouldBeNull();
        (await new Instance().Rooms.RestoreAsync(ada.Token!, new string(changed))).ShouldBeNull();
        (await new Instance().Rooms.RestoreAsync("not-a-player", checkpoint)).ShouldBeNull();
        (await new Instance().Rooms.RestoreAsync(ada.Token!, "hello")).ShouldBeNull();
        (await new Instance().Rooms.RestoreAsync(ada.Token!, checkpoint)).ShouldNotBeNull();
    }

    /// <summary>The first one back may have missed the last moves: a teammate's newer restore point adds them.</summary>
    [Fact]
    public async Task A_more_recent_restore_point_brings_the_rebuilt_match_forward()
    {
        var before = new Instance();
        var (room, ada, bea) = await StartedMatchAsync(before);
        await before.PlayMovesAsync(room, 4);
        string older = before.Notifier.Last("c-ada").Checkpoint!;
        await before.PlayMovesAsync(room, 6);
        var newest = before.Notifier.Last("c-bea");

        var after = new Instance();
        var restored = (await after.Rooms.RestoreAsync(ada.Token!, older)).ShouldNotBeNull();
        await restored.RejoinAsync(ada.Token!, "c-ada-2");
        await restored.RejoinAsync(bea.Token!, "c-bea-2");

        (await after.Rooms.ExtendAsync(restored, newest.Checkpoint!)).ShouldBeTrue();
        (await after.Rooms.ExtendAsync(restored, older)).ShouldBeFalse(); // never backwards

        var update = after.Notifier.Last("c-bea-2");
        update.Sequence.ShouldBe(newest.Sequence);
        Json(update.View).ShouldBe(Json(newest.View));
    }

    [Fact]
    public async Task Without_a_configured_key_restore_points_still_work_until_the_process_stops()
    {
        var before = new Instance(key: null);
        var (room, ada, _) = await StartedMatchAsync(before);
        await before.PlayMovesAsync(room, 2);

        before.Notifier.Last("c-ada").Checkpoint.ShouldNotBeNullOrEmpty();
        (await new Instance(key: null).Rooms.RestoreAsync(ada.Token!, before.Notifier.Last("c-ada").Checkpoint!)).ShouldBeNull();
    }
}
