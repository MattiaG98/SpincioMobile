using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>Who the player's matches are against, for the statistics.</summary>
public enum StatsMode
{
    Normal,
    Hard,
    Online,
}

public sealed record ModeStats(int Played = 0, int Won = 0)
{
    public double WinRate => Played == 0 ? 0 : (double)Won / Played;
}

/// <summary>The player's statistics on this device (menu "Statistiche"). Counts only moves seen live, never a replay.</summary>
public sealed record PlayerStats
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public ModeStats Normal { get; init; } = new();

    public ModeStats Hard { get; init; } = new();

    public ModeStats Online { get; init; } = new();

    /// <summary>Matches left before the end (abandoned, or replaced by a new match).</summary>
    public int Abandoned { get; init; }

    public int CurrentStreak { get; init; }

    public int BestStreak { get; init; }

    public int BiggestWinMargin { get; init; }

    public int AllCoinsWins { get; init; }

    public int Rounds { get; init; }

    /// <summary>Every point scored by the player's team: sweeps, declarations and end-of-round points.</summary>
    public int TeamPoints { get; init; }

    public int MySweeps { get; init; }

    public int MyDeclarations { get; init; }

    public int MyDeclarationPoints { get; init; }

    /// <summary>Rounds in which the player's team took the settebello, the rebello, a spincio.</summary>
    public int Settebelli { get; init; }

    public int Rebelli { get; init; }

    public int SpincioRounds { get; init; }

    public int LongestSpincio { get; init; }

    public int Played => Normal.Played + Hard.Played + Online.Played;

    public int Won => Normal.Won + Hard.Won + Online.Won;

    public double WinRate => Played == 0 ? 0 : (double)Won / Played;

    public bool IsEmpty => Played == 0 && Abandoned == 0 && Rounds == 0;

    public ModeStats For(StatsMode mode) => mode switch
    {
        StatsMode.Hard => Hard,
        StatsMode.Online => Online,
        _ => Normal,
    };

    public PlayerStats With(StatsMode mode, ModeStats value) => mode switch
    {
        StatsMode.Hard => this with { Hard = value },
        StatsMode.Online => this with { Online = value },
        _ => this with { Normal = value },
    };
}

/// <summary>Updates <see cref="PlayerStats"/> from the events the player sees (pure: no storage, no clock).</summary>
public static class StatsTracker
{
    public static PlayerStats Apply(PlayerStats stats, IEnumerable<GameEvent> events, Seat me, StatsMode mode)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(events);
        var us = me.Team;
        foreach (var e in events)
        {
            stats = e switch
            {
                CardPlayed { IsSweep: true } p when p.Seat == me => stats with { MySweeps = stats.MySweeps + 1 },
                Declared d when d.Seat == me => stats with
                {
                    MyDeclarations = stats.MyDeclarations + 1,
                    MyDeclarationPoints = stats.MyDeclarationPoints + d.Points,
                },
                RoundScored r => RoundEnded(stats, r, us),
                MatchEnded m => MatchEnded(stats, m, us, mode),
                _ => stats,
            };
        }

        return stats;
    }

    public static PlayerStats Abandon(PlayerStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return stats with { Abandoned = stats.Abandoned + 1, CurrentStreak = 0 };
    }

    private static PlayerStats RoundEnded(PlayerStats stats, RoundScored scored, Team us)
    {
        var tally = scored.Score.For(us);
        int points = tally.Total + scored.Ledger.Sweeps.For(us) + scored.Ledger.Declarations.For(us);
        return stats with
        {
            Rounds = stats.Rounds + 1,
            TeamPoints = stats.TeamPoints + points,
            Settebelli = stats.Settebelli + tally.SettebelloPoint,
            Rebelli = stats.Rebelli + tally.RebelloPoint,
            SpincioRounds = stats.SpincioRounds + (tally.SpincioPoints > 0 ? 1 : 0),
            LongestSpincio = Math.Max(stats.LongestSpincio, tally.SpincioLength),
        };
    }

    private static PlayerStats MatchEnded(PlayerStats stats, MatchEnded ended, Team us, StatsMode mode)
    {
        bool won = ended.Winner == us;
        var modeStats = stats.For(mode);
        stats = stats.With(mode, new ModeStats(modeStats.Played + 1, modeStats.Won + (won ? 1 : 0)));
        if (!won)
        {
            return stats with { CurrentStreak = 0 };
        }

        int streak = stats.CurrentStreak + 1;
        return stats with
        {
            CurrentStreak = streak,
            BestStreak = Math.Max(stats.BestStreak, streak),
            BiggestWinMargin = Math.Max(stats.BiggestWinMargin, ended.FinalScore.For(us) - ended.FinalScore.For(GameText.Other(us))),
            AllCoinsWins = stats.AllCoinsWins + (ended.Reason == WinReason.AllCoins ? 1 : 0),
        };
    }
}
