using System.Collections.Immutable;

namespace Spincio.Engine;

/// <summary>
/// A guess of what one seat cannot see: the other hands, the deck order and the captured piles.
/// Search bots (L2) sample these to simulate possible futures (ADR 0008).
/// </summary>
/// <param name="Hands">Indexed by seat. The viewer's own entry must be its real hand.</param>
/// <param name="Deck">Undealt cards in dealing order; length must equal <see cref="PlayerView.DeckCount"/>.</param>
/// <param name="Piles">Captured cards, indexed by team (remembered from public plays).</param>
/// <param name="LastCapturingTeam">Remembered from public plays (P7).</param>
public sealed record HiddenGuess(
    ImmutableArray<ImmutableArray<Card>> Hands,
    ImmutableArray<Card> Deck,
    ImmutableArray<ImmutableArray<Card>> Piles,
    Team? LastCapturingTeam);

public static partial class SpincioEngine
{
    /// <summary>
    /// Builds a hypothetical state from a seat's own <see cref="PlayerView"/> plus a guess of the hidden cards.
    /// The result agrees with everything the viewer knows (hand, table, hand sizes, deck size, score,
    /// declarations); only the hidden parts come from the guess. It never exposes the real state (ADR 0008).
    /// </summary>
    /// <exception cref="ArgumentException">The guess contradicts the view or does not account for all 40 cards.</exception>
    public static MatchState Hypothetical(PlayerView view, HiddenGuess guess, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(guess);

        if (view.Phase != MatchPhase.AwaitingPlay)
        {
            throw new ArgumentException("The match is over.", nameof(view));
        }

        if (guess.Hands.Length != Seat.Count || guess.Piles.Length != 2)
        {
            throw new ArgumentException("A guess needs 4 hands and 2 piles.", nameof(guess));
        }

        if (!guess.Hands[view.Seat.Index].Order().SequenceEqual(view.Hand.Order()))
        {
            throw new ArgumentException("The guess must keep the viewer's own hand.", nameof(guess));
        }

        for (int i = 0; i < Seat.Count; i++)
        {
            if (guess.Hands[i].Length != view.HandCounts[i])
            {
                throw new ArgumentException($"Seat {i} must hold {view.HandCounts[i]} cards.", nameof(guess));
            }
        }

        if (guess.Deck.Length != view.DeckCount)
        {
            throw new ArgumentException($"The deck must hold {view.DeckCount} cards.", nameof(guess));
        }

        var all = guess.Hands.SelectMany(h => h).Concat(view.Table).Concat(guess.Deck).Concat(guess.Piles.SelectMany(p => p)).ToList();
        if (all.Count != Card.FullDeck.Count || all.Distinct().Count() != Card.FullDeck.Count)
        {
            throw new ArgumentException("The guess must account for each of the 40 cards exactly once.", nameof(guess));
        }

        return new MatchState
        {
            Rng = Pcg32.FromSeed(seed),
            Phase = MatchPhase.AwaitingPlay,
            MatchNumber = view.MatchNumber,
            RoundNumber = view.RoundNumber,
            Dealer = view.Dealer,
            ToPlay = view.ToPlay,
            DealNumber = view.DealNumber,
            PlaysInRound = view.PlaysInRound,
            Hands = guess.Hands,
            Table = view.Table,
            Deck = guess.Deck,
            Piles = guess.Piles,
            Unclaimed = [],
            LastCapturingTeam = guess.LastCapturingTeam,
            Score = view.Score,
            Ledger = RoundLedger.Empty,

            // Every seat starts a deal with 3 cards, so fewer means it has already played in this deal.
            PlayedThisDeal = [.. view.HandCounts.Select(n => n < MatchState.CardsPerHand)],
            Declarations = view.Declarations,
            Winner = null,
            Sequence = 0,
        };
    }
}
