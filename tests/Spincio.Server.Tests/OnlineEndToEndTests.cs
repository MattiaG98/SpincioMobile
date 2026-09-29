using Microsoft.AspNetCore.Mvc.Testing;
using Spincio.Client.Game;

namespace Spincio.Server.Tests;

/// <summary>
/// The real browser-side <see cref="RemoteGameSession"/> against the real server, in memory
/// (SignalR over long polling through the test server).
/// </summary>
public class OnlineEndToEndTests
{
    private static async Task Eventually(Func<bool> condition, string what)
    {
        for (int i = 0; i < 200; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        condition().ShouldBeTrue($"Timed out waiting for: {what}");
    }

    [Fact]
    public async Task Two_friends_create_join_start_and_play_a_move()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Rooms:BotDelay", "00:00:00.020"));
        var url = factory.Server.BaseAddress;
        await using var ada = new RemoteGameSession(url, new InMemoryOnlineSeatStore(), factory.Server.CreateHandler);
        await using var bea = new RemoteGameSession(url, new InMemoryOnlineSeatStore(), factory.Server.CreateHandler);

        var created = await ada.CreateRoomAsync("Ada");
        created.Ok.ShouldBeTrue(created.Error);
        var joined = await bea.JoinRoomAsync(created.RoomCode!, "Bea");
        joined.Ok.ShouldBeTrue(joined.Error);
        joined.Seat.ShouldBe(2); // partners
        (await ada.StartMatchAsync()).Ok.ShouldBeTrue();

        await Eventually(() => ada.IsStarted && bea.IsStarted, "both players see the match");
        ada.View.Hand.Intersect(bea.View.Hand).ShouldBeEmpty();
        ada.Room!.Commitment.ShouldNotBeNullOrEmpty();
        ada.SeatName(bea.Me).ShouldBe("Bea");

        await Eventually(() => ada.View.IsMyTurn || bea.View.IsMyTurn, "a human turn");
        var player = ada.View.IsMyTurn ? ada : bea;
        int handBefore = player.View.Hand.Length;
        bool declared = player.LegalCommands[0] is Spincio.Engine.Declare;

        await player.PlayAsync(player.LegalCommands[0]);

        player.Error.ShouldBeNull();
        if (!declared)
        {
            await Eventually(() => player.View.Hand.Length < handBefore || player.View.DealNumber > 1, "the card left the hand");
        }
    }

    [Fact]
    public async Task Unknown_room_code_is_refused()
    {
        await using var factory = new WebApplicationFactory<Program>();
        await using var ada = new RemoteGameSession(factory.Server.BaseAddress, new InMemoryOnlineSeatStore(), factory.Server.CreateHandler);

        var result = await ada.JoinRoomAsync("ZZZZZ", "Ada");

        result.Ok.ShouldBeFalse();
        ada.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_player_can_come_back_with_the_remembered_seat()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Rooms:BotDelay", "00:00:00.020"));
        var store = new InMemoryOnlineSeatStore();
        var first = new RemoteGameSession(factory.Server.BaseAddress, store, factory.Server.CreateHandler);
        var created = await first.CreateRoomAsync("Ada");
        await first.StartMatchAsync();
        await Eventually(() => first.IsStarted, "match started");
        await first.DisposeAsync(); // tab closed

        await using var second = new RemoteGameSession(factory.Server.BaseAddress, store, factory.Server.CreateHandler);
        (await second.TryRejoinAsync()).ShouldBeTrue();

        await Eventually(() => second.IsStarted, "snapshot received");
        second.Me.Index.ShouldBe(created.Seat);
        second.Room!.Code.ShouldBe(created.RoomCode);
    }
}
