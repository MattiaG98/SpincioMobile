using System.Diagnostics;
using System.Globalization;
using Spincio.Bots;
using Spincio.Simulator;

// Usage: Spincio.Simulator [--x Greedy] [--y Random] [--matches 1000] [--seed 1] [--worlds 16]
//        Spincio.Simulator --timing Pimc [--worlds 16]   (average decision time)
var options = args.Chunk(2).Where(p => p.Length == 2).ToDictionary(p => p[0].TrimStart('-'), p => p[1], StringComparer.OrdinalIgnoreCase);

int worlds = int.Parse(options.GetValueOrDefault("worlds", PimcOptions.Default.Worlds.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
var pimc = PimcOptions.Default with { Worlds = worlds };
ulong seed = ulong.Parse(options.GetValueOrDefault("seed", "1"), CultureInfo.InvariantCulture);

if (options.TryGetValue("timing", out var timed))
{
    var bot = BotFactory.Create(Enum.Parse<BotLevel>(timed, ignoreCase: true), pimc);
    var timing = DecisionTiming.Measure(bot, seed, matches: 3);
    Console.WriteLine(timing);
    return;
}

var x = Enum.Parse<BotLevel>(options.GetValueOrDefault("x", nameof(BotLevel.Greedy)), ignoreCase: true);
var y = Enum.Parse<BotLevel>(options.GetValueOrDefault("y", nameof(BotLevel.Random)), ignoreCase: true);
int matches = int.Parse(options.GetValueOrDefault("matches", "1000"), CultureInfo.InvariantCulture);

var stopwatch = Stopwatch.StartNew();
var result = Tournament.Run(BotFactory.Create(x, pimc), BotFactory.Create(y, pimc), matches, seed);
stopwatch.Stop();

Console.WriteLine(result);
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"seed {seed}, worlds {worlds}, {stopwatch.Elapsed.TotalSeconds:F1} s"));
