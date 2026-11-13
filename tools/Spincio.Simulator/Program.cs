using System.Diagnostics;
using System.Globalization;
using Spincio.Bots;
using Spincio.Engine;
using Spincio.Simulator;

// Usage: Spincio.Simulator [--x Greedy] [--y Random] [--matches 1000] [--seed 1] [--worlds 16] [--yworlds N] [--variant Spincione]
//        (levels: Random, Greedy, Pimc, Lookahead; L3 also takes --budget N --plies N, --ybudget N --yplies N)
//        Spincio.Simulator --timing Pimc [--worlds 16]   (average decision time)
//        Spincio.Simulator --report report.json [--worlds 64]   (how the CPU weighed a reported move)
//        Spincio.Simulator --audit 1 --x Pimc --worlds 4 --y Greedy --matches 200   (mistakes judged with the real hands)
var options = args.Chunk(2).Where(p => p.Length == 2).ToDictionary(p => p[0].TrimStart('-'), p => p[1], StringComparer.OrdinalIgnoreCase);

int worlds = int.Parse(options.GetValueOrDefault("worlds", PimcOptions.Default.Worlds.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
var pimc = PimcOptions.Default with { Worlds = worlds };
// Bot Y may search a different number of worlds (e.g. L2 with 16 worlds against L2 with 8).
var pimcY = options.TryGetValue("yworlds", out var yWorlds) ? pimc with { Worlds = int.Parse(yWorlds, CultureInfo.InvariantCulture) } : pimc;
ulong seed = ulong.Parse(options.GetValueOrDefault("seed", "1"), CultureInfo.InvariantCulture);

double Number(string key, double fallback) =>
    options.TryGetValue(key, out var text) ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;

// L3: --worlds as above, plus --budget (positions per decision) and --plies (longest search); --y... for bot Y.
var lookahead = LookaheadOptions.Default with
{
    Worlds = worlds,
    NodeBudget = (long)Number("budget", LookaheadOptions.Default.NodeBudget),
    MaxPlies = (int)Number("plies", LookaheadOptions.Default.MaxPlies),
};
var lookaheadY = lookahead with
{
    Worlds = pimcY.Worlds,
    NodeBudget = (long)Number("ybudget", lookahead.NodeBudget),
    MaxPlies = (int)Number("yplies", lookahead.MaxPlies),
};

if (options.TryGetValue("report", out var reportFile))
{
    // A player's report about a CPU move (ADR 0020), saved from the issue's JSON block.
    Console.Write(ReportReplay.Explain(File.ReadAllText(reportFile), options.ContainsKey("worlds") ? worlds : 64));
    return;
}

var variant = Enum.Parse<GameVariant>(options.GetValueOrDefault("variant", nameof(GameVariant.Spincio)), ignoreCase: true);

if (options.TryGetValue("timing", out var timed))
{
    var bot = BotFactory.Create(Enum.Parse<BotLevel>(timed, ignoreCase: true), pimc, lookahead);
    var timing = DecisionTiming.Measure(bot, seed, matches: 3, variant);
    Console.WriteLine(timing);
    return;
}

var x = Enum.Parse<BotLevel>(options.GetValueOrDefault("x", nameof(BotLevel.Greedy)), ignoreCase: true);
var y = Enum.Parse<BotLevel>(options.GetValueOrDefault("y", nameof(BotLevel.Random)), ignoreCase: true);
int matches = int.Parse(options.GetValueOrDefault("matches", "1000"), CultureInfo.InvariantCulture);

if (options.ContainsKey("audit"))
{
    Console.WriteLine(Audit.Run(BotFactory.Create(x, pimc, lookahead), BotFactory.Create(y, pimcY, lookaheadY), matches, seed, variant));
    return;
}

var stopwatch = Stopwatch.StartNew();
var result = Tournament.Run(BotFactory.Create(x, pimc, lookahead), BotFactory.Create(y, pimcY, lookaheadY), matches, seed, variant);
stopwatch.Stop();

Console.WriteLine(result);
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{variant}, seed {seed}, worlds {worlds}/{pimcY.Worlds}, {stopwatch.Elapsed.TotalSeconds:F1} s"));
