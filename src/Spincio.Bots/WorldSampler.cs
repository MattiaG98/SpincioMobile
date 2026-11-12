using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// Deals the cards a seat has not seen into one possible world (ADR 0008), at random but consistent with what the seat
/// knows: hand sizes, the declarations made this deal (A1) and, with <paramref name="inferSilence"/>, the silences of
/// those who played their first card of the deal without declaring (A3: they had nothing to declare).
/// </summary>
/// <param name="attempts">Tries per world to satisfy the declarations and silences before settling for any world.</param>
internal sealed class WorldSampler(int attempts, bool inferSilence)
{
    /// <param name="unseen">The seat's unseen cards (<see cref="BotMemory.Unseen"/>), as many as the hidden hands and deck hold.</param>
    public HiddenGuess Sample(PlayerView view, BotMemory memory, Card[] unseen, ref Pcg32 rng)
    {
        var declared = view.Declarations
            .Where(d => d.DealNumber == view.DealNumber && d.Seat != view.Seat)
            .ToList();
        var silent = inferSilence
            ? Seat.All.Where(s => s != view.Seat && !memory.PlayedThisDeal(s).IsEmpty && !declared.Exists(d => d.Seat == s)).ToList()
            : [];

        HiddenGuess? guess = null;
        for (int attempt = 0; attempt < Math.Max(1, attempts); attempt++)
        {
            guess = Deal(view, memory, rng.Shuffle(unseen));
            if (declared.TrueForAll(d => Consistent(d, guess, memory, view.Variant))
                && silent.TrueForAll(s => NothingToDeclare(s, guess, memory, view.Variant, view.DealNumber)))
            {
                return guess;
            }
        }

        return guess!; // best effort: an unconstrained world
    }

    /// <summary>True if the memory accounts for exactly the cards hidden from the view (false e.g. for a memory started mid-round).</summary>
    public static bool Fits(PlayerView view, Card[] unseen) =>
        unseen.Length == view.HandCounts.Where((_, i) => i != view.Seat.Index).Sum() + view.DeckCount;

    private static bool NothingToDeclare(Seat seat, HiddenGuess guess, BotMemory memory, GameVariant variant, int dealNumber) =>
        Declarations.Evaluate(memory.PlayedThisDeal(seat).AddRange(guess.Hands[seat.Index]), variant, dealNumber).Points == 0;

    private static bool Consistent(DeclarationRecord declaration, HiddenGuess guess, BotMemory memory, GameVariant variant)
    {
        var dealtHand = memory.PlayedThisDeal(declaration.Seat).AddRange(guess.Hands[declaration.Seat.Index]);
        var value = Declarations.Evaluate(dealtHand, variant, declaration.DealNumber);
        return value.Kind == declaration.Kind && value.Points == declaration.Points;
    }

    private static HiddenGuess Deal(PlayerView view, BotMemory memory, Card[] shuffled)
    {
        var hands = new ImmutableArray<Card>[Seat.Count];
        int position = 0;
        foreach (var seat in Seat.All)
        {
            if (seat == view.Seat)
            {
                hands[seat.Index] = view.Hand;
                continue;
            }

            int count = view.HandCounts[seat.Index];
            hands[seat.Index] = [.. shuffled.AsSpan(position, count)];
            position += count;
        }

        return new HiddenGuess(
            [.. hands],
            [.. shuffled.AsSpan(position)],
            [memory.CapturedBy(Team.A), memory.CapturedBy(Team.B)],
            memory.LastCapturingTeam);
    }
}
