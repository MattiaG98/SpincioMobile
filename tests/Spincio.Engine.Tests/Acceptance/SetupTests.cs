using Spincio.Engine;
using Spincio.Engine.Tests.Support;

namespace Spincio.Engine.Tests.Acceptance;

public class SetupTests
{
    [Fact]
    public void AT_01_new_round_deals_three_cards_each_four_on_table_24_in_deck()
    {
        var state = SpincioEngine.NewMatch(seed: 1).State;

        state.Hands.ShouldAllBe(h => h.Length == 3);
        state.Table.Length.ShouldBe(4);
        state.Deck.Length.ShouldBe(24);
        state.AllCards().Order().ShouldBe(Card.FullDeck);
    }

    [Fact]
    public void AT_02_two_or_more_aces_on_table_reshuffles_deterministically()
    {
        ulong seed = Enumerable.Range(0, 5000).Select(s => (ulong)s)
            .First(s => SpincioEngine.NewMatch(s).EventsOf<DeckReshuffled>().Length > 0);

        var first = SpincioEngine.NewMatch(seed);
        var second = SpincioEngine.NewMatch(seed);

        first.EventsOf<DeckReshuffled>().ShouldAllBe(e => e.RejectedTable.Count(c => c.Rank == Rank.Ace) >= 2);
        first.State.Table.Count(c => c.Rank == Rank.Ace).ShouldBeLessThanOrEqualTo(1);
        Playout.Snapshot(first).ShouldBe(Playout.Snapshot(second));
    }

    [Fact]
    public void AT_02_reshuffle_keeps_the_same_dealer()
    {
        ulong seed = Enumerable.Range(0, 5000).Select(s => (ulong)s)
            .First(s => SpincioEngine.NewMatch(s, new Seat(1)).EventsOf<DeckReshuffled>().Length > 0);

        var start = SpincioEngine.NewMatch(seed, new Seat(1));

        start.EventsOf<RoundStarted>().ShouldHaveSingleItem().Dealer.ShouldBe(new Seat(1));
        start.State.Dealer.ShouldBe(new Seat(1));
    }

    [Fact]
    public void AT_03_seat_after_dealer_plays_first_and_dealer_rotates()
    {
        var steps = Playout.RandomMatch(seed: 3, firstDealer: new Seat(1));

        steps[0].State.ToPlay.ShouldBe(new Seat(2));
        var rounds = steps.SelectMany(t => t.EventsOf<RoundStarted>()).ToList();
        rounds.Count.ShouldBeGreaterThanOrEqualTo(2);
        rounds[0].Dealer.ShouldBe(new Seat(1));
        rounds[1].Dealer.ShouldBe(new Seat(2));
    }

    [Fact]
    public void AT_04_same_seed_and_commands_give_identical_states_and_events()
    {
        var first = Playout.RandomMatch(seed: 4).Select(Playout.Snapshot).ToList();
        var second = Playout.RandomMatch(seed: 4).Select(Playout.Snapshot).ToList();

        second.ShouldBe(first);
    }

    [Fact]
    public void First_dealer_is_random_when_not_given()
    {
        var dealers = Enumerable.Range(0, 200)
            .Select(s => SpincioEngine.NewMatch((ulong)s).State.Dealer)
            .Distinct()
            .Count();

        dealers.ShouldBe(4);
    }
}
