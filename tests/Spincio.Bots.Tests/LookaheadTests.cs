using FsCheck.Xunit;
using Spincio.Engine;
using Spincio.Simulator;
using static Spincio.Engine.Tests.Support.Cards;

namespace Spincio.Bots.Tests;

/// <summary>L3 (ADR 0019): its compact rules, its view of the round points, its play.</summary>
public class LookaheadTests
{
    private const double AllCoinsValue = 30;

    /// <summary>
    /// The compact rules of <see cref="DealSearch"/> must give exactly the value of the same search played with the
    /// engine itself, on every play of whole rounds: sweeps, the ace, the last play, new deals, the round points and the
    /// Spincione's extras.
    /// </summary>
    [Theory]
    [InlineData(GameVariant.Spincio, 11UL)]
    [InlineData(GameVariant.Spincio, 21UL)]
    [InlineData(GameVariant.Spincione, 12UL)]
    public void The_compact_search_agrees_with_the_engine(GameVariant variant, ulong seed)
    {
        var greedy = new GreedyBot();
        var memories = Seat.All.Select(s => new BotMemory(s, variant)).ToArray();
        var rng = new Pcg32(seed, 1);
        var search = new DealSearch(variant, AllCoinsValue);
        var transition = SpincioEngine.NewMatch(seed, variant: variant);
        int compared = 0, rounds = 0;

        while (transition.State.Phase == MatchPhase.AwaitingPlay && rounds < 2)
        {
            foreach (var memory in memories)
            {
                memory.Observe(transition.EventsFor(memory.Seat));
            }

            var state = transition.State;
            var seat = state.ToPlay;
            var plays = SpincioEngine.LegalCommands(state, seat).OfType<PlayCard>().ToList();
            int plies = Math.Min(state.Hands.Sum(h => h.Length) + 1, LookaheadOptions.Default.MaxPlies) - 1;
            search.Load(state).ShouldBeTrue();
            foreach (var play in plays)
            {
                double expected = Reference(state, SpincioEngine.Apply(state, play).Value, plies, seat.Team);
                search.Evaluate(play, seat.Team, plies).ShouldBe(expected, 1e-9, $"{play} at play {state.PlaysInRound}");
                compared++;
            }

            var view = SpincioEngine.ViewFor(state, seat);
            transition = SpincioEngine.Apply(state, greedy.Choose(view, memories[seat.Index], ref rng)).Value;
            rounds += transition.Events.Count(e => e is RoundScored);
        }

        compared.ShouldBeGreaterThan(100);
    }

    [Fact]
    public void The_exact_round_points_are_the_engine_s()
    {
        var rng = new Pcg32(5, 5);
        foreach (var variant in new[] { GameVariant.Spincio, GameVariant.Spincione })
        {
            for (int i = 0; i < 300; i++)
            {
                var deck = rng.Shuffle(variant.Deck());
                int split = i % 3 == 0 ? variant.CardCount() / 2 : rng.NextInt(variant.CardCount() + 1);
                var a = deck[..split];
                var b = deck[split..];
                var score = RoundScoring.Score(a, b, variant);
                double bonus = score.AllCoinsTeam is { } coins ? (coins == Team.A ? AllCoinsValue : -AllCoinsValue) : 0;

                RoundOutlook.Exact(CardTally.Of(a), CardTally.Of(b), variant, AllCoinsValue)
                    .ShouldBe(score.A.Total - score.B.Total + bonus);
            }
        }
    }

    [Theory]
    [InlineData(21, 10, 9, 1.0)] // the cards point is won
    [InlineData(10, 21, 9, -1.0)]
    [InlineData(20, 20, 0, 0.0)] // 20-20: nobody (C13)
    [InlineData(0, 0, 40, 0.0)] // nothing taken yet: even
    [InlineData(5, 0, 1, 1.0)] // coins 5-0 with one left: at worst 5-1
    public void A_majority_is_worth_what_is_still_open(int mine, int theirs, int remaining, double expected)
    {
        RoundOutlook.Majority(mine, theirs, remaining).ShouldBe(expected, 1e-12);
    }

    [Fact]
    public void A_half_won_majority_is_worth_more_than_nothing_and_less_than_the_point()
    {
        double value = RoundOutlook.Majority(5, 2, 10);

        value.ShouldBeInRange(0.5, 0.99);
        RoundOutlook.Majority(2, 5, 10).ShouldBe(-value, 1e-12);
    }

    [Fact]
    public void The_spincio_is_worth_what_its_run_can_still_become()
    {
        const int ace = 1, two = 2, three = 4, four = 8;

        RoundOutlook.Spincio(ace | two | three, four, copies: 1).ShouldBe(3, 1e-12); // the four is gone: exactly 3
        RoundOutlook.Spincio(two | three, ace, copies: 1).ShouldBe(0); // no ace, no spincio
        RoundOutlook.Spincio(ace | two | three, 0, copies: 1).ShouldBeGreaterThan(3); // it may still grow
        RoundOutlook.Spincio(ace | two, three, copies: 2).ShouldBeGreaterThan(0); // Spincione: the other three may come
        RoundOutlook.Spincio(ace | two, 0, copies: 1).ShouldBeGreaterThan(RoundOutlook.Spincio(ace, 0, copies: 1));
    }

    /// <summary>At the start of a round: the 7 of cups takes the settebello, the other cards would only be dropped.</summary>
    [Fact]
    public void Takes_the_settebello()
    {
        var state = SpincioEngine.NewMatch(seed: 4).State with
        {
            Hands = [Many("7C,2S,4B"), Many("2C,3C,4C"), Many("2D,3D,4D"), Many("5C,5S,5B")],
            Table = Many("7D,KS,6C,JB"),
            ToPlay = new Seat(0),
            PlaysInRound = 0,
            DealNumber = 1,
        };
        state = state with { Deck = [.. Card.FullDeck.Except(state.Hands.SelectMany(h => h)).Except(state.Table)] };
        var view = SpincioEngine.ViewFor(state, new Seat(0));
        var memory = new BotMemory(new Seat(0));
        memory.Observe(new HandDealt(new Seat(0), view.Hand));
        memory.Observe(new DealStarted(1, view.Table, view.DeckCount));
        var rng = new Pcg32(1, 1);

        var chosen = (PlayCard)new LookaheadBot(LookaheadOptions.Default with { Worlds = 8 }).Choose(view, memory, ref rng);

        chosen.Card.ShouldBe(C("7C"));
        chosen.Capture.ShouldBe(Take("7D"));
    }

    /// <summary>Any illegal command makes <see cref="MatchRunner"/> throw.</summary>
    [Property(MaxTest = 6)]
    public void Plays_only_legal_commands_and_matches_end(ulong seed)
    {
        IBot l3 = new LookaheadBot(LookaheadOptions.Default with { Worlds = 2 }), l1 = new GreedyBot();

        MatchRunner.Play(seed, [l3, l1, l3, l1]).Commands.ShouldBeGreaterThan(0);
        MatchRunner.Play(seed, [l1, l3, l1, l3], variant: GameVariant.Spincione).Commands.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Beats_l2_as_it_plays_on_normal()
    {
        var result = Tournament.Run(
            new LookaheadBot(LookaheadOptions.Default with { Worlds = 8, NodeBudget = 60_000 }),
            new PimcBot(PimcOptions.Default with { Worlds = 4 }),
            matches: 200,
            seed: 2026);

        result.WinRateX.ShouldBeGreaterThan(0.55, result.ToString());
    }

    /// <summary>
    /// The search of <see cref="DealSearch"/> played with the engine: same depth, same values (round points when the
    /// round ends, expected round points otherwise), no pruning.
    /// </summary>
    private static double Reference(MatchState root, Transition step, int pliesLeft, Team us)
    {
        var them = us == Team.A ? Team.B : Team.A;
        if (step.Events.OfType<RoundScored>().FirstOrDefault() is { } scored)
        {
            double bonus = scored.Score.AllCoinsTeam is { } coins ? (coins == us ? AllCoinsValue : -AllCoinsValue) : 0;
            return (scored.MatchScore.For(us) - root.Score.For(us)) - (scored.MatchScore.For(them) - root.Score.For(them)) + bonus;
        }

        var state = step.State;
        var plays = SpincioEngine.LegalCommands(state, state.ToPlay).OfType<PlayCard>().ToList();
        if (pliesLeft <= 0 || plays.Count == 0)
        {
            double live = (state.Score.For(us) - root.Score.For(us)) - (state.Score.For(them) - root.Score.For(them));
            double outlook = Outlook(state) - Outlook(root);
            return live + (us == Team.A ? outlook : -outlook);
        }

        var values = plays.Select(p => Reference(root, SpincioEngine.Apply(state, p).Value, pliesLeft - 1, us));
        return state.ToPlay.Team == us ? values.Max() : values.Min();
    }

    private static double Outlook(MatchState state) => RoundOutlook.Expected(
        CardTally.Of(state.PileOf(Team.A)), CardTally.Of(state.PileOf(Team.B)), state.Variant, AllCoinsValue);
}
