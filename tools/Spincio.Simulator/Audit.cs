using System.Globalization;
using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Simulator;

/// <summary>How often a bot makes mistakes a good player would not, judged with the real hands (the bot never sees them).</summary>
public sealed record AuditResult(
    string Bot,
    int Decisions,
    int GaveSweep,
    int MissedSweep,
    int MissedSettebello,
    int GaveSettebello,
    int DroppedSeven,
    int Rounds,
    int SweepsMade,
    int SweepsConceded)
{
    public override string ToString()
    {
        string Rate(int count) => (100.0 * count / Math.Max(1, Decisions)).ToString("F2", CultureInfo.InvariantCulture) + "%";
        string PerRound(int count) => ((double)count / Math.Max(1, Rounds)).ToString("F2", CultureInfo.InvariantCulture);
        return $"{Bot}: {Decisions} decisions with a choice | gave a sweep {Rate(GaveSweep)}, missed a sweep {Rate(MissedSweep)}, " +
            $"missed the settebello {Rate(MissedSettebello)}, left the settebello to the next opponent {Rate(GaveSettebello)}, " +
            $"dropped a seven {Rate(DroppedSeven)} | per round: sweeps made {PerRound(SweepsMade)}, conceded {PerRound(SweepsConceded)}";
    }
}

/// <summary>
/// Plays bot X against bot Y (sides alternate) and judges every decision of X that had a real choice against the
/// real cards: a mistake is a move that gives the next opponent something another legal move would not have given,
/// or misses something another move would have taken. The harness sees the real state; the bots do not (ADR 0007).
/// </summary>
public static class Audit
{
    public static AuditResult Run(IBot x, IBot y, int matches, ulong seed, GameVariant variant = GameVariant.Spincio)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        var counts = new Counts();
        for (int i = 0; i < matches; i++)
        {
            bool xIsTeamA = i % 2 == 0;
            IBot[] seats = xIsTeamA ? [x, y, x, y] : [y, x, y, x];
            Play(seed + (ulong)i, seats, xIsTeamA ? Team.A : Team.B, variant, counts);
        }

        return new AuditResult(
            x.Name, counts.Decisions, counts.GaveSweep, counts.MissedSweep, counts.MissedSettebello, counts.GaveSettebello,
            counts.DroppedSeven, counts.Rounds, counts.SweepsMade, counts.SweepsConceded);
    }

    private static void Play(ulong seed, IBot[] bots, Team audited, GameVariant variant, Counts counts)
    {
        var memories = Seat.All.Select(s => new BotMemory(s, variant)).ToArray();
        var rngs = Seat.All.Select(s => new Pcg32(seed, 100UL + (ulong)s.Index)).ToArray();
        var transition = SpincioEngine.NewMatch(seed, variant: variant);
        Observe(transition, memories);

        while (transition.State.Phase != MatchPhase.MatchOver)
        {
            var state = transition.State;
            var seat = state.ToPlay;
            var command = bots[seat.Index].Choose(SpincioEngine.ViewFor(state, seat), memories[seat.Index], ref rngs[seat.Index]);
            if (seat.Team == audited && command is PlayCard chosen)
            {
                Judge(state, chosen, counts);
            }

            transition = SpincioEngine.Apply(state, command).Value;
            Observe(transition, memories);
            foreach (var e in transition.Events)
            {
                switch (e)
                {
                    case CardPlayed { IsSweep: true } p when p.Seat.Team == audited:
                        counts.SweepsMade++;
                        break;
                    case CardPlayed { IsSweep: true }:
                        counts.SweepsConceded++;
                        break;
                    case RoundScored:
                        counts.Rounds++;
                        break;
                }
            }
        }
    }

    private static void Judge(MatchState state, PlayCard chosen, Counts counts)
    {
        var plays = SpincioEngine.LegalCommands(state, state.ToPlay).OfType<PlayCard>().ToList();
        if (plays.Count < 2)
        {
            return;
        }

        counts.Decisions++;
        var outcomes = plays.Select(p => Outcome(state, p)).ToList();
        var mine = outcomes[plays.IndexOf(chosen)];

        if (mine.GivesSweep && outcomes.Exists(o => !o.GivesSweep))
        {
            counts.GaveSweep++;
        }

        if (!mine.Sweeps && outcomes.Exists(o => o.Sweeps))
        {
            counts.MissedSweep++;
        }

        if (!mine.TakesSettebello && !mine.Sweeps && outcomes.Exists(o => o.TakesSettebello))
        {
            counts.MissedSettebello++;
        }

        if (mine.GivesSettebello && outcomes.Exists(o => !o.GivesSettebello))
        {
            counts.GaveSettebello++;
        }

        if (chosen.Capture is null && chosen.Card.Rank == Rank.Seven
            && plays.Exists(p => p.Capture is not null || p.Card.Rank != Rank.Seven))
        {
            counts.DroppedSeven++;
        }
    }

    private sealed record PlayOutcome(bool Sweeps, bool TakesSettebello, bool GivesSweep, bool GivesSettebello);

    private static PlayOutcome Outcome(MatchState state, PlayCard play)
    {
        var step = SpincioEngine.Apply(state, play).Value;
        var played = step.Events.OfType<CardPlayed>().Single();
        bool sweeps = played.IsSweep;
        bool takesSettebello = played.Captured.Contains(RoundScoring.Settebello) || (played.Card == RoundScoring.Settebello && !played.Captured.IsEmpty);

        // What the next seat (an opponent) can do with its real hand, if the round goes on.
        bool givesSweep = false, givesSettebello = false;
        var next = step.State;
        if (next.Phase == MatchPhase.AwaitingPlay && !step.Events.OfType<RoundScored>().Any())
        {
            foreach (var reply in SpincioEngine.LegalCommands(next, next.ToPlay).OfType<PlayCard>())
            {
                var answer = SpincioEngine.Apply(next, reply).Value.Events.OfType<CardPlayed>().Single();
                givesSweep |= answer.IsSweep;
                givesSettebello |= answer.Captured.Contains(RoundScoring.Settebello);
            }
        }

        return new PlayOutcome(sweeps, takesSettebello, givesSweep, givesSettebello);
    }

    private static void Observe(Transition transition, BotMemory[] memories)
    {
        foreach (var memory in memories)
        {
            memory.Observe(transition.EventsFor(memory.Seat));
        }
    }

    private sealed class Counts
    {
        public int Decisions { get; set; }

        public int GaveSweep { get; set; }

        public int MissedSweep { get; set; }

        public int MissedSettebello { get; set; }

        public int GaveSettebello { get; set; }

        public int DroppedSeven { get; set; }

        public int Rounds { get; set; }

        public int SweepsMade { get; set; }

        public int SweepsConceded { get; set; }
    }
}
