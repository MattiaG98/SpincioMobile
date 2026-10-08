using Spincio.Engine;

namespace Spincio.Client.Game;

public enum PointsKind
{
    Sweep,
    Declaration,
    RoundEnd,

    /// <summary>A Mariana that does not empty the table: the banner only, no points.</summary>
    Mariana,
}

/// <summary>
/// Points that fly to a team's score (presentation only): "+N" appears where they were made and joins the score.
/// </summary>
/// <param name="From">Where they appear, relative to the viewer: 0 = me, 1 = right, 2 = partner, 3 = left; <see cref="Table"/> = the table.</param>
/// <param name="Ours">True for the viewer's team ("Noi"), false for the other one ("Loro").</param>
/// <param name="Label">What earned them, shown with the points (null = points only).</param>
public sealed record PointsGain(PointsKind Kind, int From, bool Ours, int Points, string? Label)
{
    public const int Table = -1;

    /// <summary>
    /// Points scored live by an event: a sweep (P6, "Spazzino!" or "MARIANA") or a declaration (A1); a Mariana that does
    /// not sweep shows its banner with no points. Null for anything else.
    /// </summary>
    public static PointsGain? For(GameEvent e, Seat viewer) => e switch
    {
        CardPlayed { IsSweep: true } p => new(PointsKind.Sweep, Table, p.Seat.Team == viewer.Team, 1, GameText.Banner(p)),
        CardPlayed p when p.IsMariana() => new(PointsKind.Mariana, Table, p.Seat.Team == viewer.Team, 0, GameText.Banner(p)),
        Declared { Points: > 0 } d => new(PointsKind.Declaration, GameText.Relative(d.Seat, viewer), d.Seat.Team == viewer.Team, d.Points, GameText.DeclarationName(d.Kind)),
        _ => null,
    };

    /// <summary>End-of-round points (F1–F6), from the table: one entry per team that scored, ours first.</summary>
    public static IReadOnlyList<PointsGain> ForRound(RoundScored scored, Team us)
    {
        ArgumentNullException.ThrowIfNull(scored);
        var gains = new List<PointsGain>(2);
        foreach (var team in new[] { us, GameText.Other(us) })
        {
            int points = scored.Score.For(team).Total;
            if (points > 0)
            {
                gains.Add(new(PointsKind.RoundEnd, Table, team == us, points, null));
            }
        }

        return gains;
    }

    /// <summary>The match score before the end-of-round points: the score bar keeps it until they have flown in.</summary>
    public static TeamScores ScoreBefore(RoundScored scored)
    {
        ArgumentNullException.ThrowIfNull(scored);
        return new(scored.MatchScore.A - scored.Score.A.Total, scored.MatchScore.B - scored.Score.B.Total);
    }
}
