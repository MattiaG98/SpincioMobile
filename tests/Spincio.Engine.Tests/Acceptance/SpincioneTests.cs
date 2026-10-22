using System.Security.Cryptography;
using System.Text;
using Spincio.Engine;
using Spincio.Engine.Tests.Support;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Engine.Tests.Acceptance;

/// <summary>Spincione, the two-deck mode (SPEC §1.7, SP1–SP10).</summary>
public class SpincioneTests
{
    private static Scenario Spincione() => new() { Variant = GameVariant.Spincione, DealNumber = 2 };

    [Fact]
    public void AT_41_a_round_starts_with_4_cards_each_4_on_the_table_and_60_in_the_deck()
    {
        var state = SpincioEngine.NewMatch(seed: 1, variant: GameVariant.Spincione).State;

        state.Variant.ShouldBe(GameVariant.Spincione);
        state.Hands.Select(h => h.Length).ShouldBe([4, 4, 4, 4]);
        state.Table.Length.ShouldBe(4);
        state.Deck.Length.ShouldBe(60);
        var counts = state.AllCards().GroupBy(c => c).ToList();
        counts.Count.ShouldBe(40);
        counts.Select(g => g.Count()).Distinct().ShouldBe([2]);
    }

    [Fact]
    public void AT_42_later_deals_give_3_cards_and_a_round_has_6_deals_and_76_plays()
    {
        int plays = 0, deals = 0;
        var handSizes = new List<int>();
        var events = Playout.RandomMatch(seed: 3, variant: GameVariant.Spincione).SelectMany(s => s.Events)
            .TakeWhile(e => e is not RoundScored); // the first round only
        foreach (var e in events)
        {
            switch (e)
            {
                case HandDealt dealt when dealt.Seat.Index == 0:
                    handSizes.Add(dealt.Cards.Length);
                    break;
                case DealStarted:
                    deals++;
                    break;
                case CardPlayed:
                    plays++;
                    break;
            }
        }

        deals.ShouldBe(6);
        plays.ShouldBe(76);
        handSizes.ShouldBe([4, 3, 3, 3, 3, 3]);
    }

    [Theory]
    [InlineData("3D,4D", "7D", "3D,4D", 2)] // sweep + same suit
    [InlineData("3D,4C", "7D", "3D,4C", 1)] // sweep only
    [InlineData("3S,5S,KB", "AS", "3S,5S,KB", 1)] // ace takes all, a club among them: sweep only
    [InlineData("3S,5S", "AS", "3S,5S", 2)] // ace takes all, all swords: sweep + same suit
    [InlineData("2D,3D,KC", "5D", "2D,3D", 1)] // same suit, no sweep
    public void AT_43_a_capture_all_of_one_suit_is_worth_a_point(string table, string played, string captured, int points)
    {
        var state = Spincione().Hand(0, played).Table(table).Build();

        var step = state.Ok(new PlayCard(new Seat(0), C(played), Take(captured)));

        var play = step.EventsOf<CardPlayed>().Single();
        play.Points.ShouldBe(points);
        step.State.Score.A.ShouldBe(points);
        step.State.Ledger.SameSuit.A.ShouldBe(play.IsSameSuit ? 1 : 0);
    }

    [Fact]
    public void AT_44_the_same_suit_point_counts_on_the_last_play_and_only_in_the_spincione()
    {
        var last = new Scenario { Variant = GameVariant.Spincione }.Hand(0, "7D").Table("3D,4D").LastPlay(Scenario.LeftoverTarget.PileB).Build();
        var lastPlay = last.Ok(new PlayCard(new Seat(0), C("7D"), Take("3D,4D"))).EventsOf<CardPlayed>().Single();
        lastPlay.IsSweep.ShouldBeFalse();
        lastPlay.IsSameSuit.ShouldBeTrue();

        var spincio = new Scenario().Hand(0, "7D").Table("3D,4D,KC").Build();
        var spincioPlay = spincio.Ok(new PlayCard(new Seat(0), C("7D"), Take("3D,4D"))).EventsOf<CardPlayed>().Single();
        spincioPlay.IsSameSuit.ShouldBeFalse();
        spincioPlay.Points.ShouldBe(0);
    }

    [Theory]
    [InlineData("AC,2S,5B,2D", 4)]
    [InlineData("2C,2S,2B,4D", 6)]
    [InlineData("3C,3S,3B,3D", 14)]
    [InlineData("2C,2S,2B,2D", 18)]
    [InlineData("AC,AS,2B,2D", 4)]
    [InlineData("5C,5S,NB,ND", 0)]
    [InlineData("7C,7S,7B,KD", 0)]
    [InlineData("5D,7D,ND,KD", 4)]
    [InlineData("JC,NS,KB,AD", 4)]
    [InlineData("KC,AS,2B,3D", 4)]
    [InlineData("AD,2C,3S,4B", 8)]
    public void AT_45_first_hand_declarations_with_4_cards_are_doubled(string hand, int points)
    {
        Declarations.Evaluate(Many(hand), GameVariant.Spincione, dealNumber: 1).Points.ShouldBe(points);
    }

    [Theory]
    [InlineData("AD,2D,3D", 6, DeclarationKind.LowSum | DeclarationKind.Colore | DeclarationKind.Scala)]
    [InlineData("NC,KS,AB", 2, DeclarationKind.Scala)]
    [InlineData("KC,AC,2C", 4, DeclarationKind.Scala | DeclarationKind.Colore)]
    [InlineData("2C,2S,5B", 3, DeclarationKind.LowSumWithPair)]
    [InlineData("3C,3S,3B", 9, DeclarationKind.ThreeOfAKind | DeclarationKind.LowSum)]
    [InlineData("5D,5D,ND", 2, DeclarationKind.Colore)]
    public void AT_46_later_declarations_are_the_spincio_ones_plus_colore_and_scala(string hand, int points, DeclarationKind kind)
    {
        Declarations.Evaluate(Many(hand), GameVariant.Spincione, dealNumber: 3).ShouldBe(new DeclarationValue(kind, points));
    }

    [Fact]
    public void AT_46_a_first_hand_declaration_is_made_and_scored_doubled()
    {
        var state = new Scenario { Variant = GameVariant.Spincione, DealNumber = 1 }.Hand(0, "AD,2D,3D,4D").Build();

        var step = state.Ok(new Declare(new Seat(0)));

        // somma 10 (2) + colore (2) + scala (2) = 6, doubled.
        step.EventsOf<Declared>().Single().Points.ShouldBe(12);
        step.State.Score.A.ShouldBe(12);
    }

    [Fact]
    public void AT_47_settebello_and_rebello_count_once_per_copy_and_even_splits_score_nothing()
    {
        // A: both settebelli and one copy of every other card (41 cards, 11 coins, 5 sevens); B: the other copies.
        var pileA = new List<Card>(Card.FullDeck) { RoundScoring.Settebello };
        var pileB = Card.FullDeck.Where(c => c != RoundScoring.Settebello).ToList();

        // Even out cards, coins and sevens, keeping the settebelli with A and a rebello each.
        void Move(List<Card> from, List<Card> to, string card)
        {
            from.Remove(C(card)).ShouldBeTrue();
            to.Add(C(card));
        }

        Move(pileA, pileB, "2D"); // coins 10-10, cards 40-40
        Move(pileA, pileB, "7C"); // sevens 4-4
        Move(pileB, pileA, "3C"); // cards 40-40 again

        var score = RoundScoring.Score(pileA, pileB, GameVariant.Spincione);

        score.A.SettebelloPoint.ShouldBe(2);
        score.B.SettebelloPoint.ShouldBe(0);
        score.A.RebelloPoint.ShouldBe(1);
        score.B.RebelloPoint.ShouldBe(1);
        (score.A.CardsPoint, score.B.CardsPoint).ShouldBe((0, 0)); // 40-40
        (score.A.CoinsPoint, score.B.CoinsPoint).ShouldBe((0, 0)); // 10-10
        (score.A.PrimieraPoint, score.B.PrimieraPoint).ShouldBe((0, 0)); // 4-4
    }

    [Fact]
    public void AT_48_all_the_coins_do_not_win_at_once_in_the_spincione()
    {
        var deck = GameVariant.Spincione.Deck();
        var pileA = deck.Where(c => c.IsCoins).ToList();
        var pileB = deck.Where(c => !c.IsCoins).ToList();

        var score = RoundScoring.Score(pileA, pileB, GameVariant.Spincione);

        score.AllCoinsTeam.ShouldBeNull();
        score.A.SpincioPoints.ShouldBe(10);
        MatchRules.Evaluate(new TeamScores(5, 20), score.AllCoinsTeam).Kind.ShouldBe(MatchOutcomeKind.Continue);
    }

    [Fact]
    public void AT_49_two_identical_cards_on_the_table_are_one_choice()
    {
        var state = Spincione().Hand(0, "5S").Table("5D,5D").Build();

        var plays = SpincioEngine.LegalCommands(state, new Seat(0)).OfType<PlayCard>().ToList();

        plays.ShouldHaveSingleItem().Capture!.Cards.ShouldBe(Many("5D"));
        var step = state.Ok(plays[0]);
        step.State.Table.ShouldBe(Many("5D"));
    }

    [Fact]
    public void AT_49_identical_cards_do_not_repeat_sum_captures()
    {
        var state = Spincione().Hand(0, "6S").Table("2D,2D,4C").Build();

        var captures = SpincioEngine.LegalCommands(state, new Seat(0)).OfType<PlayCard>().Select(p => p.Capture!.ToString()).ToList();

        captures.ShouldBe(["{2D,4C}"]); // 2D+2D+... would be 4, not 6; the two 2D+4C sets are the same choice
    }

    [Fact]
    public void AT_50_the_variant_is_part_of_the_match_and_the_spincio_did_not_change()
    {
        string Fingerprint(GameVariant variant)
        {
            var text = new StringBuilder();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                foreach (var step in Playout.RandomMatch(seed, variant: variant))
                {
                    var s = step.State;
                    text.Append(string.Join(",", s.Hands.Select(h => string.Join(" ", h)))).Append('|').Append(string.Join(" ", s.Table))
                        .Append('|').Append(s.Score).Append('|').Append(s.Sequence).Append('|').Append(s.Phase).Append(';');
                    foreach (var e in step.Events)
                    {
                        text.Append(e.GetType().Name);
                        if (e is CardPlayed p)
                        {
                            text.Append(p.Seat).Append(p.Card).Append(string.Join("", p.Captured)).Append(p.IsSweep);
                        }

                        text.Append(',');
                    }
                }
            }

            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        // Rules v1.7 (the ace sweeps on the last play, AT-52). Before it, 3e295c7…7e81c, measured with the engine of 1.8.1
        // before the Spincione existed; turning the v1.7 rule off for the Spincio gives that value back.
        Fingerprint(GameVariant.Spincio).ShouldBe("0ecafd223e2578d559175e7d6cf70ae61976a502d8fb9b0cca91b87b529a58d1");
        Fingerprint(GameVariant.Spincione).ShouldBe(Fingerprint(GameVariant.Spincione));
        Fingerprint(GameVariant.Spincione).ShouldNotBe(Fingerprint(GameVariant.Spincio));
    }

    [Theory]
    [InlineData("5D,KC", "5D", "5D", 2, true)] // the twin: 2, instead of the same-suit point
    [InlineData("5D", "5D", "5D", 3, true)] // the twin empties the table: 2 + the sweep ("gemella con cappella")
    [InlineData("5D,5C,KC", "5D", "5D", 2, true)] // two fives on the initial table: taking the twin is worth 2…
    [InlineData("5D,5C,KC", "5D", "5C", 0, false)] // …taking the other five is worth nothing
    [InlineData("AD,3C", "AD", "AD", 2, true)] // an ace takes only the ace on the table (P8): its twin
    public void AT_51_a_card_taking_its_twin_is_worth_2_points_and_3_with_the_sweep(
        string table, string played, string captured, int points, bool twin)
    {
        var state = Spincione().Hand(0, played).Table(table).Build();
        var legal = SpincioEngine.LegalCommands(state, new Seat(0)).OfType<PlayCard>().Select(p => p.Capture?.ToString()).ToList();
        legal.ShouldContain(Take(captured).ToString());

        var step = state.Ok(new PlayCard(new Seat(0), C(played), Take(captured)));

        var play = step.EventsOf<CardPlayed>().Single();
        play.IsTwin.ShouldBe(twin);
        play.IsSameSuit.ShouldBeFalse(); // the twin scores its own points, not the same-suit one
        play.Points.ShouldBe(points);
        step.State.Score.A.ShouldBe(points);
        step.State.Ledger.Twins.A.ShouldBe(twin ? 2 : 0);
        step.State.Ledger.SameSuit.A.ShouldBe(0);
    }

    [Fact]
    public void AT_51_the_twin_counts_on_the_last_play_without_the_sweep()
    {
        var last = new Scenario { Variant = GameVariant.Spincione }.Hand(0, "5D").Table("5D").LastPlay(Scenario.LeftoverTarget.PileB).Build();

        var step = last.Ok(new PlayCard(new Seat(0), C("5D"), Take("5D")));

        var play = step.EventsOf<CardPlayed>().Single();
        play.IsSweep.ShouldBeFalse();
        play.IsTwin.ShouldBeTrue();
        play.Points.ShouldBe(2);
        step.EventsOf<RoundScored>().Single().Ledger.Twins.A.ShouldBe(2);
    }
}
