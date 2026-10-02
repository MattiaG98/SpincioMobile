using Spincio.Bots;
using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

/// <summary>M10: every card play is animated on the board as it was before the move, then the move is shown.</summary>
public class MoveAnimationTests
{
    /// <summary>Records what the board looked like when each animation started.</summary>
    private sealed class RecordingAnimator(LocalGameSession session) : IMoveAnimator
    {
        public List<(CardPlayed Play, int Position, PlayerView Before)> Plays { get; } = [];

        public int Settles { get; private set; }

        public Task PlayAsync(CardPlayed play, int position)
        {
            Plays.Add((play, position, session.View));
            return Task.CompletedTask;
        }

        public Task SettleAsync()
        {
            Settles++;
            return Task.CompletedTask;
        }
    }

    private static async Task<(LocalGameSession Session, RecordingAnimator Animator)> PlayAMatchAsync(ulong seed)
    {
        var session = new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask);
        var animator = new RecordingAnimator(session);
        session.Animator = animator;
        await session.NewGameAsync(seed);

        var bot = new GreedyBot();
        var memory = new BotMemory(LocalGameSession.Human);
        var rng = Pcg32.FromSeed(7);
        while (session.Result is null)
        {
            if (session.PendingSummary is not null)
            {
                await session.AcknowledgeSummaryAsync();
                continue;
            }

            await session.PlayAsync(bot.Choose(session.View, memory, ref rng));
        }

        return (session, animator);
    }

    [Fact]
    public async Task Every_play_is_animated_before_the_board_changes()
    {
        var (_, animator) = await PlayAMatchAsync(seed: 21);

        animator.Plays.Count.ShouldBeGreaterThan(40);
        animator.Settles.ShouldBe(animator.Plays.Count);
        var wrong = animator.Plays.Where(p =>
            !p.Play.Captured.All(p.Before.Table.Contains)   // captured cards are still on the table
            || p.Before.Table.Contains(p.Play.Card)).ToList(); // the played card is not there yet
        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task Positions_are_relative_to_the_viewer_and_my_cards_start_from_my_hand()
    {
        var (_, animator) = await PlayAMatchAsync(seed: 22);

        animator.Plays.Select(p => p.Position).Distinct().Order().ShouldBe([0, 1, 2, 3]);
        var mine = animator.Plays.Where(p => p.Position == 0).ToList();
        mine.ShouldNotBeEmpty();
        mine.Where(p => !p.Before.Hand.Contains(p.Play.Card)).ShouldBeEmpty();
        animator.Plays.Where(p => p.Position != GameText.Relative(p.Play.Seat, LocalGameSession.Human)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_an_animator_the_game_is_unchanged()
    {
        var (animated, _) = await PlayAMatchAsync(seed: 23);
        var plain = new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask);
        await plain.NewGameAsync(seed: 23);
        var bot = new GreedyBot();
        var memory = new BotMemory(LocalGameSession.Human);
        var rng = Pcg32.FromSeed(7);
        while (plain.Result is null)
        {
            if (plain.PendingSummary is not null)
            {
                await plain.AcknowledgeSummaryAsync();
                continue;
            }

            await plain.PlayAsync(bot.Choose(plain.View, memory, ref rng));
        }

        plain.Result.ShouldBe(animated.Result);
        plain.Feed.ShouldBe(animated.Feed);
    }
}
