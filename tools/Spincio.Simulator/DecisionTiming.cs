using System.Diagnostics;
using System.Globalization;
using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Simulator;

public sealed record TimingResult(string Bot, int Decisions, double AverageMs, double MaxMs)
{
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"{Bot}: {Decisions} decisions, avg {AverageMs:F1} ms, max {MaxMs:F1} ms");
}

/// <summary>Measures how long a bot takes per decision when it plays all four seats.</summary>
public static class DecisionTiming
{
    public static TimingResult Measure(IBot bot, ulong seed, int matches)
    {
        ArgumentNullException.ThrowIfNull(bot);
        var times = new List<double>();
        var timed = new TimedBot(bot, times);
        for (int i = 0; i < matches; i++)
        {
            MatchRunner.Play(seed + (ulong)i, [timed, timed, timed, timed]);
        }

        return new TimingResult(bot.Name, times.Count, times.Average(), times.Max());
    }

    private sealed class TimedBot(IBot inner, List<double> times) : IBot
    {
        public string Name => inner.Name;

        public Command Choose(PlayerView view, BotMemory memory, ref Pcg32 rng)
        {
            var stopwatch = Stopwatch.StartNew();
            var command = inner.Choose(view, memory, ref rng);
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
            return command;
        }
    }
}
