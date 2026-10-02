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
