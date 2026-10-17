using FsCheck.Xunit;
using Spincio.Engine;
using Spincio.Engine.Tests.Support;

namespace Spincio.Engine.Tests;

/// <summary>Invariants from CLAUDE.md, checked over whole random matches.</summary>
public class PropertyTests
{
    [Property(MaxTest = 100)]
    public void The_40_cards_are_conserved(ulong seed)
    {
        Playout.RandomMatch(seed, step => step.State.AllCards().Order().ShouldBe(Card.FullDeck));
    }

    [Property(MaxTest = 50)]
    public void Same_seed_same_commands_same_states_and_events(ulong seed)
    {
        var first = Playout.RandomMatch(seed).Select(Playout.Snapshot).ToList();
        var second = Playout.RandomMatch(seed).Select(Playout.Snapshot).ToList();

        second.ShouldBe(first);
    }

    /// <summary>
    /// SPEC §5 deduction, made precise: equal-value cards can sit on the table together only if both come
    /// from the initial table of the round (a dropped card never matches a table card, or it would capture).
    /// </summary>
    [Property(MaxTest = 100)]
    public void Equal_values_on_table_only_from_the_initial_table(ulong seed)
    {
        HashSet<Card> initialTable = [];
        Playout.RandomMatch(seed, step =>
        {
            foreach (var deal in step.EventsOf<DealStarted>().Where(d => d.DealNumber == 1))
            {
                initialTable = [.. deal.Table];
            }

            // Plain LINQ: Shouldly's ShouldAllBe compiles an expression tree per call, too slow per step.
            var strays = step.State.Table.GroupBy(c => c.Value).Where(g => g.Count() > 1).SelectMany(g => g)
                .Where(c => !initialTable.Contains(c));
            strays.ShouldBeEmpty();
        });
    }

    [Property(MaxTest = 100)]
    public void Legal_commands_exist_and_are_accepted_until_the_match_ends(ulong seed)
    {
        Playout.RandomMatch(seed, step =>
        {
            if (step.State.Phase == MatchPhase.MatchOver)
            {
                return;
            }

            var legal = SpincioEngine.LegalCommands(step.State, step.State.ToPlay);
            legal.ShouldNotBeEmpty();
            legal.Where(c => !SpincioEngine.Apply(step.State, c).IsSuccess).ShouldBeEmpty();
        });
    }

    [Property(MaxTest = 50)]
    public void Legal_commands_from_the_view_match_those_from_the_state(ulong seed)
    {
        Playout.RandomMatch(seed, step =>
        {
            foreach (var seat in Seat.All)
            {
                var fromView = SpincioEngine.LegalCommands(SpincioEngine.ViewFor(step.State, seat));
                var fromState = SpincioEngine.LegalCommands(step.State, seat);
                fromView.SequenceEqual(fromState).ShouldBeTrue();
            }
        });
    }

    [Property(MaxTest = 50)]
    public void Every_round_has_three_deals_and_36_plays(ulong seed)
    {
        int plays = 0, deals = 0;
        bool inRound = false;
        Playout.RandomMatch(seed, step =>
        {
            foreach (var e in step.Events)
            {
                switch (e)
                {
                    case RoundStarted:
                        inRound = true;
                        plays = 0;
                        deals = 0;
                        break;
                    case DealStarted:
                        deals++;
                        break;
                    case CardPlayed:
                        plays++;
                        break;
                    case RoundScored:
                        inRound.ShouldBeTrue();
                        plays.ShouldBe(MatchState.PlaysPerRound);
                        deals.ShouldBe(3);
                        break;
                }
            }
        });
    }

    // Spincione (SPEC §1.7): the same invariants with two decks, where every card exists twice.

    [Property(MaxTest = 30)]
    public void Spincione_the_80_cards_are_conserved(ulong seed)
    {
        var deck = GameVariant.Spincione.Deck().Order().ToList();
        Playout.RandomMatch(seed, step => step.State.AllCards().Order().SequenceEqual(deck).ShouldBeTrue(), variant: GameVariant.Spincione);
    }

    [Property(MaxTest = 20)]
    public void Spincione_same_seed_same_commands_same_states_and_events(ulong seed)
    {
        var first = Playout.RandomMatch(seed, variant: GameVariant.Spincione).Select(Playout.Snapshot).ToList();
        var second = Playout.RandomMatch(seed, variant: GameVariant.Spincione).Select(Playout.Snapshot).ToList();

        second.ShouldBe(first);
    }

    [Property(MaxTest = 30)]
    public void Spincione_legal_commands_exist_are_accepted_and_match_the_view(ulong seed)
    {
        Playout.RandomMatch(seed, step =>
        {
            if (step.State.Phase == MatchPhase.MatchOver)
            {
                return;
            }

            var legal = SpincioEngine.LegalCommands(step.State, step.State.ToPlay);
            legal.ShouldNotBeEmpty();
            legal.Where(c => !SpincioEngine.Apply(step.State, c).IsSuccess).ShouldBeEmpty();
            legal.Distinct().Count().ShouldBe(legal.Count); // identical cards never repeat a choice
            SpincioEngine.LegalCommands(SpincioEngine.ViewFor(step.State, step.State.ToPlay)).SequenceEqual(legal).ShouldBeTrue();
        }, variant: GameVariant.Spincione);
    }

    /// <summary>The §5 deduction holds with two decks too, counting copies: a dropped card never matches the table.</summary>
    [Property(MaxTest = 30)]
    public void Spincione_equal_values_on_table_only_from_the_initial_table(ulong seed)
    {
        List<Card> initialTable = [];
        Playout.RandomMatch(seed, step =>
        {
            foreach (var deal in step.EventsOf<DealStarted>().Where(d => d.DealNumber == 1))
            {
                initialTable = [.. deal.Table];
            }

            var strays = step.State.Table.GroupBy(c => c.Value).Where(g => g.Count() > 1).SelectMany(g => g)
                .GroupBy(c => c).Where(g => g.Count() > initialTable.Count(c => c == g.Key));
            strays.ShouldBeEmpty();
        }, variant: GameVariant.Spincione);
    }

    [Property(MaxTest = 20)]
    public void Spincione_every_round_has_six_deals_and_76_plays(ulong seed)
    {
        int plays = 0, deals = 0;
        Playout.RandomMatch(seed, step =>
        {
            foreach (var e in step.Events)
            {
                switch (e)
                {
                    case RoundStarted:
                        plays = 0;
                        deals = 0;
                        break;
                    case DealStarted:
                        deals++;
                        break;
                    case CardPlayed:
                        plays++;
                        break;
                    case RoundScored:
                        plays.ShouldBe(76);
                        deals.ShouldBe(6);
                        break;
                }
            }
        }, variant: GameVariant.Spincione);
    }
}
