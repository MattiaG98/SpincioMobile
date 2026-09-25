using System.Diagnostics;
using System.Globalization;
using Spincio.Bots;
using Spincio.Simulator;

// Usage: Spincio.Simulator [--x Greedy] [--y Random] [--matches 1000] [--seed 1]
var options = args.Chunk(2).Where(p => p.Length == 2).ToDictionary(p => p[0].TrimStart('-'), p => p[1], StringComparer.OrdinalIgnoreCase);

var x = Enum.Parse<BotLevel>(options.GetValueOrDefault("x", nameof(BotLevel.Greedy)), ignoreCase: true);
var y = Enum.Parse<BotLevel>(options.GetValueOrDefault("y", nameof(BotLevel.Random)), ignoreCase: true);
int matches = int.Parse(options.GetValueOrDefault("matches", "1000"), CultureInfo.InvariantCulture);
ulong seed = ulong.Parse(options.GetValueOrDefault("seed", "1"), CultureInfo.InvariantCulture);

var stopwatch = Stopwatch.StartNew();
var result = Tournament.Run(BotFactory.Create(x), BotFactory.Create(y), matches, seed);
stopwatch.Stop();

Console.WriteLine(result);
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"seed {seed}, {stopwatch.Elapsed.TotalSeconds:F1} s"));
