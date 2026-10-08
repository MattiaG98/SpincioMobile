using FsCheck.Xunit;
using Spincio.Engine;
using Spincio.Simulator;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Bots.Tests;

public class PimcBotTests
{
    private static readonly PimcOptions Fast = PimcOptions.Default with { Worlds = 3 };

    [Fact]
    public void Always_declares_when_it_can()
    {
        var view = Views.For("2C,2S,5B", "KD", declarable: new DeclarationValue(DeclarationKind.LowSumWithPair, 3));
        var rng = Pcg32.FromSeed(1);

        new PimcBot(Fast).Choose(view, Views.Memory("2C,2S,5B,KD"), ref rng).ShouldBe(new Declare(new Seat(0)));
    }

    /// <summary>Any illegal command makes <see cref="MatchRunner"/> throw.</summary>
    [Property(MaxTest = 4)]
    public void Plays_only_legal_commands_against_L1(ulong seed)
    {
        IBot l2 = new PimcBot(Fast), l1 = new GreedyBot();

        MatchRunner.Play(seed, [l2, l1, l2, l1]).Commands.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Same_seed_same_match()
    {
        IBot l2 = new PimcBot(Fast), l1 = new GreedyBot();

        MatchRunner.Play(9, [l2, l1, l2, l1]).ShouldBe(MatchRunner.Play(9, [l2, l1, l2, l1]));
    }

    [Fact]
    public void Sampled_worlds_honour_a_declaration_made_this_deal()
    {
        // Seat 1 declared "fino a 9 con coppia" (3 points) and has already played 2C this deal:
        // its two remaining cards must complete a pair with total ≤ 9 together with 2C.
        var memory = new BotMemory(new Seat(0));
        memory.Observe(new RoundStarted(0, 1, new Seat(3)));
        memory.Observe(new HandDealt(new Seat(0), Many("KD,KC,KS")));
        memory.Observe(new DealStarted(1, Many("JD,JC,JS,JB"), 24));
        memory.Observe(new Declared(new Seat(1), DeclarationKind.LowSumWithPair, 3));
        memory.Observe(new CardPlayed(new Seat(1), C("2C"), [], IsSweep: false));

        var view = Views.For("KD,KC,KS", "JD,JC,JS,JB,2C", playsInRound: 1, handCounts: [3, 2, 3, 3]) with
        {
            DeckCount = 24,
            Declarations = [new DeclarationRecord(new Seat(1), 1, DeclarationKind.LowSumWithPair, 3)],
        };
        var unseen = memory.Unseen().ToArray();
        var bot = new PimcBot(PimcOptions.Default with { SamplingAttempts = 2000 });
        var rng = Pcg32.FromSeed(4);

        for (int i = 0; i < 20; i++)
        {
            var guess = bot.SampleWorld(view, memory, unseen, ref rng);
            var dealt = guess.Hands[1].Add(C("2C"));
            Declarations.Evaluate(dealt).ShouldBe(new DeclarationValue(DeclarationKind.LowSumWithPair, 3));
            SpincioEngine.Hypothetical(view, guess, 1).ShouldNotBeNull(); // consistent with the view
        }
    }

    [Fact]
    public void Sampled_worlds_honour_a_silence_this_deal()
    {
        // Seat 1 played its first card (AC) without declaring: with AC its dealt hand had nothing to declare, so it
        // cannot hold, say, A + 2 or a pair of low cards. Without the inference such hands come up often.
        var memory = new BotMemory(new Seat(0));
        memory.Observe(new RoundStarted(0, 1, new Seat(3)));
        memory.Observe(new HandDealt(new Seat(0), Many("KD,KC,KS")));
        memory.Observe(new DealStarted(1, Many("JD,JC,JS,JB"), 24));
        memory.Observe(new CardPlayed(new Seat(1), C("AC"), [], IsSweep: false));

        var view = Views.For("KD,KC,KS", "JD,JC,JS,JB,AC", playsInRound: 1, handCounts: [3, 2, 3, 3]) with { DeckCount = 24 };
        var unseen = memory.Unseen().ToArray();
        var rng = Pcg32.FromSeed(5);

        int Declarable(PimcBot bot, ref Pcg32 random)
        {
            int count = 0;
            for (int i = 0; i < 200; i++)
            {
                var guess = bot.SampleWorld(view, memory, unseen, ref random);
                count += Declarations.Evaluate(guess.Hands[1].Add(C("AC"))).Points > 0 ? 1 : 0;
            }

            return count;
        }

        Declarable(new PimcBot(PimcOptions.Default with { InferSilence = false }), ref rng).ShouldBeGreaterThan(10);
        Declarable(new PimcBot(PimcOptions.Default with { SamplingAttempts = 2000 }), ref rng).ShouldBe(0);
    }

    [Fact]
    public void Takes_a_sweep_when_one_is_available()
    {
        // First play of the round (dealer seat 3): KB takes AC+2S+3B+4D and sweeps the table.
        var memory = new BotMemory(new Seat(0));
        memory.Observe(new RoundStarted(0, 1, new Seat(3)));
        memory.Observe(new HandDealt(new Seat(0), Many("KB,5C,6S")));
        memory.Observe(new DealStarted(1, Many("AC,2S,3B,4D"), 24));

        var view = Views.For("KB,5C,6S", "AC,2S,3B,4D", playsInRound: 0) with { DeckCount = 24 };
        var rng = Pcg32.FromSeed(2);

        new PimcBot(PimcOptions.Default with { Worlds = 8 }).Choose(view, memory, ref rng)
            .ShouldBe(new PlayCard(new Seat(0), C("KB"), Take("AC,2S,3B,4D")));
    }

    [Fact]
    public void Falls_back_to_L1_when_memory_does_not_match_the_view()
    {
        var view = Views.For("KC,3S", "2B");
        var rng = Pcg32.FromSeed(1);

        // An empty memory cannot account for the cards on the table: L2 must not crash.
        new PimcBot(Fast).Choose(view, new BotMemory(new Seat(0)), ref rng).ShouldBeOfType<PlayCard>();
    }
}
