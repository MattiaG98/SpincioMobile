using Microsoft.JSInterop;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// Shows each card play before the new state appears (M10): the card flies from its player to the table and,
/// on a capture, carries the captured cards to the player who took them. Pure presentation: the engine and the
/// state are unaffected, and a session without an animator behaves exactly as before.
/// </summary>
public interface IMoveAnimator
{
    /// <summary>Animates one play on the board that still shows the state before it.</summary>
    /// <param name="position">Seat of the player relative to the viewer: 0 = me, 1 = right, 2 = partner, 3 = left.</param>
    Task PlayAsync(CardPlayed play, int position);

    /// <summary>Points scored: each "+N" flies to its team's score, still showing the score before them.</summary>
    Task PointsAsync(IReadOnlyList<PointsGain> gains);

    /// <summary>Called once the new state has rendered: removes what is left of the animation.</summary>
    Task SettleAsync();
}

/// <summary>Runs the animations in the browser (<c>wwwroot/js/moves.js</c>).</summary>
public sealed class JsMoveAnimator(IJSRuntime js) : IMoveAnimator
{
    public Task PlayAsync(CardPlayed play, int position)
    {
        ArgumentNullException.ThrowIfNull(play);
        return InvokeAsync("spincioMoves.play", play.Card.ToString(), position, play.Captured.Select(c => c.ToString()).ToArray());
    }

    public Task PointsAsync(IReadOnlyList<PointsGain> gains)
    {
        ArgumentNullException.ThrowIfNull(gains);
        // One argument, the array: passed bare, the array would become the params array (one argument per gain).
        object payload = gains.Select(g => new
        {
            kind = g.Kind.ToString().ToLowerInvariant(),
            from = g.From,
            ours = g.Ours,
            points = g.Points,
            label = g.Label,
        }).ToArray();
        return InvokeAsync("spincioMoves.points", payload);
    }

    public Task SettleAsync() => InvokeAsync("spincioMoves.settle");

    // An animation must never break the game: if the page is gone or the script is missing, just skip it.
    private async Task InvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (Exception e) when (e is JSException or JSDisconnectedException or TaskCanceledException or InvalidOperationException)
        {
        }
    }
}

public static class MoveAnimations
{
    /// <summary>
    /// Plays, in order, the animations for the events of one move on the board that still shows the state before it:
    /// card plays, then the points they score (sweeps, declarations). True if anything was animated.
    /// </summary>
    public static async Task<bool> AnimateMoveAsync(this IMoveAnimator animator, IEnumerable<GameEvent> events, Seat viewer)
    {
        ArgumentNullException.ThrowIfNull(animator);
        ArgumentNullException.ThrowIfNull(events);
        bool animated = false;
        foreach (var e in events)
        {
            if (e is CardPlayed play)
            {
                await animator.PlayAsync(play, GameText.Relative(play.Seat, viewer));
                animated = true;
            }

            if (PointsGain.For(e, viewer) is { } gain)
            {
                await animator.PointsAsync([gain]);
                animated = true;
            }
        }

        return animated;
    }
}
