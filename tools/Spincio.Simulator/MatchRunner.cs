using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Simulator;

public sealed record MatchResult(
    ulong Seed, Team Winner, WinReason Reason, TeamScores FinalScore, int Rounds, int Tiebreaks, int Commands);

/// <summary>
/// Plays a full match between bots. Each bot gets only its own view and the events addressed to its seat,
/// and its own PCG32 stream derived from the match seed, so a run is fully reproducible.
/// </summary>
public static class MatchRunner
{
    public const int MaxCommands = 50_000;

    /// <param name="bots">One bot per seat, indexed by seat.</param>
    public static MatchResult Play(ulong seed, IReadOnlyList<IBot> bots, Seat? firstDealer = null)
    {
        ArgumentNullException.ThrowIfNull(bots);
        if (bots.Count != Seat.Count)
        {
            throw new ArgumentException($"Expected {Seat.Count} bots.", nameof(bots));
        }

        var memories = Seat.All.Select(s => new BotMemory(s)).ToArray();
        var rngs = Seat.All.Select(s => new Pcg32(seed, 100UL + (ulong)s.Index)).ToArray();

        var transition = SpincioEngine.NewMatch(seed, firstDealer);
        Observe(transition, memories);
        int rounds = 1, tiebreaks = 0, commands = 0;

        while (transition.State.Phase != MatchPhase.MatchOver)
        {
            if (++commands > MaxCommands)
            {
                throw new InvalidOperationException($"Seed {seed}: match did not end within {MaxCommands} commands.");
            }

            var seat = transition.State.ToPlay;
            var bot = bots[seat.Index];
            var command = bot.Choose(SpincioEngine.ViewFor(transition.State, seat), memories[seat.Index], ref rngs[seat.Index]);
            var result = SpincioEngine.Apply(transition.State, command);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Seed {seed}: {bot.Name} at {seat} chose illegal {command}: {result.Error}");
            }

            transition = result.Value;
            Observe(transition, memories);
            rounds += transition.Events.OfType<RoundStarted>().Count();
            tiebreaks += transition.Events.OfType<TiebreakStarted>().Count();
        }

        var end = transition.Events.OfType<MatchEnded>().Single();
        return new MatchResult(seed, end.Winner, end.Reason, end.FinalScore, rounds, tiebreaks, commands);
    }

    private static void Observe(Transition transition, BotMemory[] memories)
    {
        foreach (var memory in memories)
        {
            memory.Observe(transition.EventsFor(memory.Seat));
        }
    }
}
