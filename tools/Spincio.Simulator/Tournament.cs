using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Simulator;

public sealed record TournamentResult(
    string BotX,
    string BotY,
    int Matches,
    int WinsX,
    int WinsByAllCoins,
    double AverageRounds,
    int Tiebreaks,
    double AverageScoreDifferenceX)
{
    public double WinRateX => Matches == 0 ? 0 : (double)WinsX / Matches;

    public override string ToString() =>
        $"{BotX} vs {BotY}: {Matches} matches, {BotX} wins {WinsX} ({WinRateX:P1}); " +
        $"all-coins wins {WinsByAllCoins}; avg rounds {AverageRounds:F2}; tiebreaks {Tiebreaks}; " +
        $"avg score diff for {BotX} {AverageScoreDifferenceX:+0.0;-0.0}";
}

/// <summary>Bot X as one team against bot Y; sides alternate every match to cancel seat bias.</summary>
public static class Tournament
{
    public static TournamentResult Run(IBot x, IBot y, int matches, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        int winsX = 0, allCoins = 0, rounds = 0, tiebreaks = 0;
        long scoreDiff = 0;
        for (int i = 0; i < matches; i++)
        {
            bool xIsTeamA = i % 2 == 0;
            IBot[] seats = xIsTeamA ? [x, y, x, y] : [y, x, y, x];
            var result = MatchRunner.Play(seed + (ulong)i, seats);

            var teamX = xIsTeamA ? Team.A : Team.B;
            var teamY = xIsTeamA ? Team.B : Team.A;
            if (result.Winner == teamX)
            {
                winsX++;
            }

            if (result.Reason == WinReason.AllCoins)
            {
                allCoins++;
            }

            rounds += result.Rounds;
            tiebreaks += result.Tiebreaks;
            scoreDiff += result.FinalScore.For(teamX) - result.FinalScore.For(teamY);
        }

        return new TournamentResult(
            x.Name, y.Name, matches, winsX, allCoins,
            matches == 0 ? 0 : (double)rounds / matches,
            tiebreaks,
            matches == 0 ? 0 : (double)scoreDiff / matches);
    }
}
