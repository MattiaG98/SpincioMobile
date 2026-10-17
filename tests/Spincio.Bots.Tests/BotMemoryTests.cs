using System.Collections.Immutable;
using Spincio.Engine;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Bots.Tests;

public class BotMemoryTests
{
    [Fact]
    public void Remembers_table_own_hand_and_played_cards()
    {
        var memory = new BotMemory(new Seat(0));

        memory.Observe(new RoundStarted(0, 1, new Seat(3)));
        memory.Observe(new HandDealt(new Seat(0), Many("AC,2C,3C")));
        memory.Observe(new DealStarted(1, Many("4C,5C,6C,7C"), 24));
        memory.Observe(new CardPlayed(new Seat(1), C("KB"), Many("4C,6C"), IsSweep: false));

        memory.Seen.ShouldBe(Many("AC,2C,3C,4C,5C,6C,7C,KB"), ignoreOrder: true);
        memory.UnseenCount.ShouldBe(32);
        memory.UnseenOfValue(4).ShouldBe(3);
    }

    [Fact]
    public void Forgets_everything_at_a_new_round()
    {
        var memory = Views.Memory("AC,2C,3C");

        memory.Observe(new RoundStarted(0, 2, new Seat(0)));

        memory.Seen.ShouldBeEmpty();
    }

    [Fact]
    public void Refuses_another_seat_private_hand()
    {
        var memory = new BotMemory(new Seat(0));

        Should.Throw<InvalidOperationException>(() => memory.Observe(new HandDealt(new Seat(1), Many("AC,2C,3C"))));
    }

    [Fact]
    public void Learns_nothing_from_declarations()
    {
        var memory = new BotMemory(new Seat(0));

        memory.Observe(new Declared(new Seat(1), DeclarationKind.ThreeOfAKind, 7));

        memory.Seen.ShouldBeEmpty();
    }

    [Fact]
    public void Initial_table_is_not_re_added_on_later_deals()
    {
        var memory = new BotMemory(new Seat(0));

        memory.Observe(new DealStarted(2, ImmutableArray<Card>.Empty, 12));

        memory.Seen.ShouldBeEmpty();
    }

    /// <summary>Spincione (SP1): every card exists twice, and a card taken from the table is not counted again.</summary>
    [Fact]
    public void Counts_both_copies_in_the_spincione()
    {
        var memory = new BotMemory(new Seat(0));

        memory.Observe(new RoundStarted(0, 1, new Seat(3)) { Variant = GameVariant.Spincione });
        memory.Observe(new HandDealt(new Seat(0), Many("7D,7D,2C,3C")));
        memory.Observe(new DealStarted(1, Many("5D,4C,6C,KB"), 60));
        memory.Observe(new CardPlayed(new Seat(1), C("5S"), Many("5D"), IsSweep: false));

        memory.Variant.ShouldBe(GameVariant.Spincione);
        memory.Seen.Count.ShouldBe(9); // 4 in hand, 4 on the table, the 5S played; the 5D was already seen
        memory.UnseenCount.ShouldBe(71);
        memory.UnseenOfValue(7).ShouldBe(6);
        memory.UnseenOfValue(5).ShouldBe(6);
        memory.Unseen().Count(c => c == C("7D")).ShouldBe(0);
        memory.Unseen().Count(c => c == C("5D")).ShouldBe(1);
        memory.Unseen().Count().ShouldBe(71);
    }
}
