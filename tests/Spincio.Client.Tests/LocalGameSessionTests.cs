using System.Text.Json;
using Spincio.Bots;
using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

public class LocalGameSessionTests
{
    private static LocalGameSession NewSession(ISavedGameStore? store = null) =>
        new(store ?? new InMemoryGameStore(), delay: _ => Task.CompletedTask);

    /// <summary>Plays the human seat with an L1 bot through the public session API only.</summary>
    private static async Task PlayHumanTurnsAsync(LocalGameSession session, int maxHumanCommands = int.MaxValue)
    {
        var bot = new GreedyBot();
        var memory = new BotMemory(LocalGameSession.Human);
        var rng = Pcg32.FromSeed(99);
        for (int i = 0; i < maxHumanCommands && session.Result is null; i++)
        {
            if (session.PendingSummary is not null)
            {
                await session.AcknowledgeSummaryAsync();
                continue;
            }

            session.View.IsMyTurn.ShouldBeTrue();
            await session.PlayAsync(bot.Choose(session.View, memory, ref rng));
            session.Error.ShouldBeNull();
        }
    }

    [Fact]
    public async Task New_game_runs_the_cpus_until_it_is_the_human_turn()
    {
        var session = NewSession();

        await session.NewGameAsync(seed: 11);

        session.View.IsMyTurn.ShouldBeTrue();
        session.View.Hand.Length.ShouldBe(3);
        session.IsCpuThinking.ShouldBeFalse();
    }

    [Fact]
    public async Task Illegal_command_is_rejected_and_nothing_changes()
    {
        var session = NewSession();
        await session.NewGameAsync(seed: 11);
        var before = JsonSerializer.Serialize(session.View);
        var notInHand = Card.FullDeck.First(c => !session.View.Hand.Contains(c) && !session.View.Table.Contains(c));

        await session.PlayAsync(new PlayCard(LocalGameSession.Human, notInHand));

        session.Error.ShouldNotBeNull();
        JsonSerializer.Serialize(session.View).ShouldBe(before);
    }

    [Fact]
    public async Task Commands_for_other_seats_are_ignored()
    {
        var session = NewSession();
        await session.NewGameAsync(seed: 11);
        var before = JsonSerializer.Serialize(session.View);

        await session.PlayAsync(new Declare(new Seat(1)));

        JsonSerializer.Serialize(session.View).ShouldBe(before);
    }

    [Fact]
    public async Task A_whole_match_can_be_played_through_the_session()
    {
        var store = new InMemoryGameStore();
        var session = NewSession(store);
        await session.NewGameAsync(seed: 5);

        await PlayHumanTurnsAsync(session);

        session.Result.ShouldNotBeNull();
        session.View.Phase.ShouldBe(MatchPhase.MatchOver);
        store.Json.ShouldBeNull(); // finished matches are not resumable
    }

    [Fact]
    public async Task Bots_wait_while_the_round_summary_is_open()
    {
        var session = NewSession();
        await session.NewGameAsync(seed: 5);

        while (session.PendingSummary is null && session.Result is null)
        {
            await PlayHumanTurnsAsync(session, maxHumanCommands: 1);
        }

        var view = JsonSerializer.Serialize(session.View);
        session.IsCpuThinking.ShouldBeFalse();
        await session.PlayAsync(new Declare(LocalGameSession.Human));
        JsonSerializer.Serialize(session.View).ShouldBe(view); // no input accepted until the summary is closed
    }

    [Fact]
    public async Task Saved_match_resumes_exactly_where_it_was()
    {
        var store = new InMemoryGameStore();
        var first = NewSession(store);
        await first.NewGameAsync(seed: 21);
        await PlayHumanTurnsAsync(first, maxHumanCommands: 10);
        if (first.PendingSummary is not null)
        {
            await first.AcknowledgeSummaryAsync();
        }

        var second = NewSession(store);
        (await second.TryResumeAsync()).ShouldBeTrue();

        second.Seed.ShouldBe(21UL);
        JsonSerializer.Serialize(second.View).ShouldBe(JsonSerializer.Serialize(first.View));
    }

    [Fact]
    public async Task Save_from_another_rules_version_is_not_resumed()
    {
        var store = new InMemoryGameStore();
        await store.SaveAsync(new SavedGame("1.1", 21, []).ToJson());

        var session = NewSession(store);

        (await session.HasSavedGameAsync()).ShouldBeFalse();
        (await session.TryResumeAsync()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"RulesVersion":"1.2","Seed":21,"Commands":["X9"]}""")]
    [InlineData("""{"RulesVersion":"1.2","Seed":21,"Commands":["P0:KD"]}""")]
    [InlineData("""{"RulesVersion":"1.2","Seed":21,"Commands":["P0:ZZ"]}""")]
    [InlineData("""{"RulesVersion":"1.2","Seed":21,"Commands":[""]}""")]
    public async Task Corrupt_or_illegal_save_is_not_resumed(string json)
    {
        var store = new InMemoryGameStore();
        await store.SaveAsync(json);

        (await NewSession(store).TryResumeAsync()).ShouldBeFalse();
    }
}
