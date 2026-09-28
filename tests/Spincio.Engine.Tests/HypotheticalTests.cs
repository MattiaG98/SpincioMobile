using FsCheck.Xunit;
using Spincio.Engine;
using Spincio.Engine.Tests.Support;

namespace Spincio.Engine.Tests;

/// <summary>ADR 0008: hypothetical states agree with the viewer's knowledge and reject contradictions.</summary>
public class HypotheticalTests
{
    private static HiddenGuess TrueGuess(MatchState state) =>
        new(state.Hands, state.Deck, state.Piles, state.LastCapturingTeam);

    /// <summary>With the true hidden cards, the hypothetical state plays out exactly like the real one.</summary>
    [Property(MaxTest = 40)]
    public void With_the_true_guess_the_future_is_the_real_future(ulong seed)
    {
        var chooser = new Pcg32(seed, 11);
        var state = SpincioEngine.NewMatch(seed).State;
        int stop = chooser.NextInt(30);
        for (int i = 0; i < stop; i++)
        {
            var legal = SpincioEngine.LegalCommands(state, state.ToPlay);
            state = state.Ok(legal[chooser.NextInt(legal.Count)]).State;
        }

        var viewer = state.ToPlay;
        var hypothetical = SpincioEngine.Hypothetical(SpincioEngine.ViewFor(state, viewer), TrueGuess(state), seed: 1);

        foreach (var seat in Seat.All)
        {
            SpincioEngine.LegalCommands(hypothetical, seat).ShouldBe(SpincioEngine.LegalCommands(state, seat));
        }

        // Play the rest of the round identically in both; the round tallies must match.
        RoundScored? real = null, guessed = null;
        while (real is null)
        {
            var legal = SpincioEngine.LegalCommands(state, state.ToPlay);
            var command = legal[chooser.NextInt(legal.Count)];
            var a = state.Ok(command);
            var b = hypothetical.Ok(command);
            real = a.EventsOf<RoundScored>().FirstOrDefault();
            guessed = b.EventsOf<RoundScored>().FirstOrDefault();
            state = a.State;
            hypothetical = b.State;
        }

        guessed.ShouldNotBeNull();
        guessed.Score.ShouldBe(real.Score);
        guessed.MatchScore.ShouldBe(real.MatchScore);
    }

    [Fact]
    public void Rejects_a_guess_that_changes_the_viewer_hand()
    {
        var state = SpincioEngine.NewMatch(3).State;
        var view = SpincioEngine.ViewFor(state, state.ToPlay);
        int me = view.Seat.Index, next = (me + 1) % 4;

        var swapped = TrueGuess(state) with
        {
            Hands = state.Hands.SetItem(me, state.Hands[next]).SetItem(next, state.Hands[me]),
        };

        Should.Throw<ArgumentException>(() => SpincioEngine.Hypothetical(view, swapped, 1));
    }

    [Fact]
    public void Rejects_a_guess_with_wrong_hand_sizes_or_deck_size()
    {
        var state = SpincioEngine.NewMatch(3).State;
        var view = SpincioEngine.ViewFor(state, state.ToPlay);
        int other = (view.Seat.Index + 1) % 4;

        var shortHand = TrueGuess(state) with
        {
            Hands = state.Hands.SetItem(other, state.Hands[other].RemoveAt(0)),
            Deck = state.Deck.Add(state.Hands[other][0]),
        };

        Should.Throw<ArgumentException>(() => SpincioEngine.Hypothetical(view, shortHand, 1));
    }

    [Fact]
    public void Rejects_a_guess_that_loses_or_duplicates_cards()
    {
        var state = SpincioEngine.NewMatch(3).State;
        var view = SpincioEngine.ViewFor(state, state.ToPlay);

        var duplicate = TrueGuess(state) with { Deck = state.Deck.SetItem(0, state.Deck[1]) };

        Should.Throw<ArgumentException>(() => SpincioEngine.Hypothetical(view, duplicate, 1));
    }

    [Fact]
    public void Keeps_what_the_viewer_knows()
    {
        var state = SpincioEngine.NewMatch(8).State;
        var view = SpincioEngine.ViewFor(state, state.ToPlay);

        var hypothetical = SpincioEngine.Hypothetical(view, TrueGuess(state), 1);
        var again = SpincioEngine.ViewFor(hypothetical, view.Seat);

        again.Hand.ShouldBe(view.Hand);
        again.Table.ShouldBe(view.Table);
        again.HandCounts.ShouldBe(view.HandCounts);
        again.DeckCount.ShouldBe(view.DeckCount);
        again.Score.ShouldBe(view.Score);
        again.AvailableDeclaration.ShouldBe(view.AvailableDeclaration);
    }
}
