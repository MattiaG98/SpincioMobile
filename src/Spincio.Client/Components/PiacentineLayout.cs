using System.Globalization;

namespace Spincio.Client.Components;

/// <summary>
/// Geometry for the Piacentine-style card faces (ADR 0011), in the 60×90 card box.
/// Swords are curved blades nested into lenses; clubs cross diagonally into a lattice; coins and cups are laid out as pips.
/// </summary>
public static class PiacentineLayout
{
    public const double CenterX = 30;
    public const double CenterY = 45;

    /// <summary>Pip positions for coins and cups, ranks 1..7.</summary>
    public static readonly (double X, double Y)[][] Pips =
    [
        [],
        [(30, 45)],
        [(30, 27), (30, 63)],
        [(30, 22), (30, 45), (30, 68)],
        [(19, 27), (41, 27), (19, 63), (41, 63)],
        [(19, 24), (41, 24), (30, 45), (19, 66), (41, 66)],
        [(19, 22), (41, 22), (19, 45), (41, 45), (19, 68), (41, 68)],
        [(19, 22), (41, 22), (30, 33.5), (19, 45), (41, 45), (19, 68), (41, 68)],
    ];

    /// <summary>How far each nested pair of curved swords bows out, for ranks 2..7.</summary>
    public static IReadOnlyList<double> SwordBows(int count) => count switch
    {
        2 => [10],
        3 => [13],
        4 => [7, 15],
        5 => [8.5, 17],
        6 => [5.5, 11.5, 17.5],
        7 => [6.5, 12.5, 18.5],
        _ => [],
    };

    public static bool HasStraightCentre(int count) => count % 2 == 1;

    /// <summary>A curved blade bowing left by <paramref name="bow"/>, tip at the top, hilt end at <paramref name="hiltY"/>.</summary>
    public static string CurvedBlade(double bow, double tipY, double hiltY)
    {
        var midY = (tipY + hiltY) / 2;
        var c = CenterX - (2 * bow);
        return F($"M{CenterX - 1.5},{hiltY} Q{c - 1.4},{midY} {CenterX},{tipY} Q{c + 1.4},{midY} {CenterX + 1.5},{hiltY} Z");
    }

    /// <summary>Outer swords are a little longer, so their hilts do not sit on top of each other.</summary>
    public static (double TipY, double HiltY) SwordSpan(int level) => (17 - (2.5 * level), 73 + (2.5 * level));

    /// <summary>Horizontal offsets of the clubs leaning each way (lattice), for ranks 2..7.</summary>
    public static IReadOnlyList<double> ClubOffsets(int count)
    {
        var perSide = count / 2;
        var offsets = new double[perSide];
        for (var i = 0; i < perSide; i++)
        {
            offsets[i] = (i - ((perSide - 1) / 2.0)) * (perSide >= 3 ? 9 : 12);
        }

        return offsets;
    }

    public const double ClubAngle = 22;

    /// <summary>A long knotted club, vertical and centred on the origin, half-length <paramref name="half"/>.</summary>
    public static string Club(double half) =>
        F($"M-1.9,{half} L-2.9,{-half + 4} C-3.2,{-half - 1} 3.2,{-half - 1} 2.9,{-half + 4} L1.9,{half} Z");

    public static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
