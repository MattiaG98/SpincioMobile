using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Server.Tests;

/// <summary>The authoritative room (ADR 0004), with fake time: no SignalR, no real waiting.</summary>
public class GameRoomTests
{
    [Fact]
    public async Task First_two_players_are_partners_and_a_fifth_is_refused()
    {
        await using var h = new RoomHarness();

        var seats = new List<int>();
        foreach (var name in new[] { "Ada", "Bea", "Cesare", "Dario" })
        {
            seats.Add((await h.JoinAsync(name, name)).Seat);
        }

        seats.ShouldBe([0, 2, 1, 3]);
        (await h.Room.JoinAsync("Elena", "Elena")).Ok.ShouldBeFalse();
        h.Notifier.Rooms["Ada"].Seats.Select(s => s.Name).ShouldBe(["Ada", "Cesare", "Bea", "Dario"]);
    }

    [Fact]
    public async Task Start_fills_empty_seats_with_cpus_and_publishes_a_commitment()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");

        (await h.Room.StartAsync(ada.Token!)).Ok.ShouldBeTrue();

        var room = h.Notifier.Rooms["c-ada"];
        room.Started.ShouldBeTrue();
        room.Commitment.ShouldNotBeNullOrEmpty();
        room.Seats.Count(s => s.IsBot).ShouldBe(3);
        (await h.Room.JoinAsync("Late", "c-late")).Ok.ShouldBeFalse();
    }

    /// <summary>The commitment is published before anyone sees a card, so it cannot depend on the deal.</summary>
    [Fact]
    public async Task The_commitment_reaches_every_player_before_the_first_cards()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.JoinAsync("Bea", "c-bea");

        (await h.Room.StartAsync(ada.Token!)).Ok.ShouldBeTrue();

        foreach (var connection in new[] { "c-ada", "c-bea" })
        {
            var kinds = h.Notifier.Sequence.Where(m => m.Connection == connection).Select(m => m.Kind).ToList();
            int commitment = kinds.IndexOf("room+commitment");
            commitment.ShouldBeGreaterThanOrEqualTo(0);
            commitment.ShouldBeLessThan(kinds.IndexOf("update"), connection);
        }
    }

    [Fact]
    public async Task Each_player_receives_only_their_own_hand()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        var bea = await h.JoinAsync("Bea", "c-bea");
        await h.Room.StartAsync(ada.Token!);

        var adaUpdate = h.Notifier.Last("c-ada");
        var beaUpdate = h.Notifier.Last("c-bea");

        adaUpdate.View.Seat.Index.ShouldBe(ada.Seat);
        adaUpdate.View.Hand.ShouldBe(h.State.HandOf(new Seat(ada.Seat)));
        adaUpdate.View.Hand.Intersect(h.State.HandOf(new Seat(bea.Seat))).ShouldBeEmpty();
        beaUpdate.View.Hand.ShouldBe(h.State.HandOf(new Seat(bea.Seat)));
        adaUpdate.Events.OfType<HandDealt>().ShouldAllBe(e => e.Seat.Index == ada.Seat);
    }

    [Fact]
    public async Task Stale_sequence_wrong_seat_and_illegal_moves_are_rejected()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.Room.StartAsync(ada.Token!);
        await h.UntilTurnOfAsync(ada.Seat);
        var update = h.Notifier.Last("c-ada");
        var legal = SpincioEngine.LegalCommands(update.View);
        var move = CommandCodec.Encode(legal[0]);

        (await h.Room.PlayAsync(ada.Token!, update.Sequence - 1, move)).Ok.ShouldBeFalse();
        (await h.Room.PlayAsync("not-a-token", update.Sequence, move)).Ok.ShouldBeFalse();
        (await h.Room.PlayAsync(ada.Token!, update.Sequence, "P1:7D")).Ok.ShouldBeFalse();
        (await h.Room.PlayAsync(ada.Token!, update.Sequence, "garbage")).Ok.ShouldBeFalse();
        h.State.Sequence.ShouldBe(update.Sequence);

        (await h.Room.PlayAsync(ada.Token!, update.Sequence, move)).Ok.ShouldBeTrue();
        h.Notifier.Last("c-ada").Sequence.ShouldBe(update.Sequence + 1);
    }

    [Fact]
    public async Task Cpus_play_after_a_short_pause_and_wait_for_people()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.Room.StartAsync(ada.Token!);

        await h.UntilTurnOfAsync(ada.Seat);

        h.State.ToPlay.Index.ShouldBe(ada.Seat);
        int sequence = h.State.Sequence;
        await h.AdvanceAsync(TimeSpan.FromSeconds(5)); // well under the turn timeout
        h.State.Sequence.ShouldBe(sequence);
        h.Notifier.Last("c-ada").TurnSecondsLeft.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_cpu_plays_for_a_player_whose_turn_times_out()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.Room.StartAsync(ada.Token!);
        await h.UntilTurnOfAsync(ada.Seat);
        int sequence = h.State.Sequence;

        await h.AdvanceAsync(h.Options.TurnTimeout + TimeSpan.FromSeconds(1));

        h.State.Sequence.ShouldBeGreaterThan(sequence);
        h.Notifier.Rooms["c-ada"].Seats[ada.Seat].IsBot.ShouldBeFalse(); // still Ada's seat
    }

    [Fact]
    public async Task Disconnected_player_is_replaced_after_the_grace_period_and_can_come_back()
    {
        await using var h = new RoomHarness();
        var ada = await h.JoinAsync("Ada", "c-ada");
        var bea = await h.JoinAsync("Bea", "c-bea");
        await h.Room.StartAsync(ada.Token!);

        await h.Room.DisconnectedAsync("c-bea");
        h.Notifier.Rooms["c-ada"].Seats[bea.Seat].Connected.ShouldBeFalse();
        await h.AdvanceAsync(h.Options.ReconnectGrace + TimeSpan.FromSeconds(1));
        h.Notifier.Rooms["c-ada"].Seats[bea.Seat].IsBot.ShouldBeTrue();

        // Bea's seat is now played by a CPU without waiting for the turn timeout.
        await h.UntilTurnOfAsync(ada.Seat);
        h.State.ToPlay.Index.ShouldBe(ada.Seat);

        var back = await h.Room.RejoinAsync(bea.Token!, "c-bea-2");
        back.Ok.ShouldBeTrue();
        back.Seat.ShouldBe(bea.Seat);
        h.Notifier.Rooms["c-ada"].Seats[bea.Seat].IsBot.ShouldBeFalse();
        var snapshot = h.Notifier.Last("c-bea-2");
        snapshot.View.Hand.ShouldBe(h.State.HandOf(new Seat(bea.Seat)));
        snapshot.Sequence.ShouldBe(h.State.Sequence);
    }

    [Fact]
    public async Task Leaving_the_lobby_frees_the_seat()
    {
        await using var h = new RoomHarness();
        await h.JoinAsync("Ada", "c-ada");
        var bea = await h.JoinAsync("Bea", "c-bea");

        await h.Room.DisconnectedAsync("c-bea");
        var cesare = await h.JoinAsync("Cesare", "c-cesare");

        cesare.Seat.ShouldBe(bea.Seat);
        (await h.Room.StartAsync(bea.Token!)).Ok.ShouldBeFalse();
    }

    [Fact]
    public async Task At_the_end_the_seed_is_revealed_and_the_match_can_be_verified()
    {
        await using var h = new RoomHarness(seed: 7);
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.Room.StartAsync(ada.Token!);
        string commitment = h.Notifier.Rooms["c-ada"].Commitment!;

        // Ada never moves: turn timeouts make the CPU play for her, so the match runs to the end.
        for (int i = 0; i < 2000 && h.State.Phase == MatchPhase.AwaitingPlay; i++)
        {
            await h.AdvanceAsync(h.Options.TurnTimeout);
        }

        h.State.Phase.ShouldBe(MatchPhase.MatchOver);
        var reveal = h.Notifier.Reveals["c-ada"];
        reveal.Seed.ShouldBe(7UL);
        Commitment.Verify(commitment, reveal, h.State.Score).ShouldBeTrue();
        Commitment.Verify(commitment, reveal with { Salt = "tampered" }, h.State.Score).ShouldBeFalse();
        Commitment.Verify(commitment, reveal, h.State.Score with { A = h.State.Score.A + 1 }).ShouldBeFalse();
    }

    [Fact]
    public async Task Room_can_use_the_l2_bot()
    {
        await using var h = new RoomHarness(botLevel: BotLevel.Pimc);
        var ada = await h.JoinAsync("Ada", "c-ada");
        await h.Room.StartAsync(ada.Token!);

        await h.UntilTurnOfAsync(ada.Seat);

        h.State.ToPlay.Index.ShouldBe(ada.Seat);
    }
}
