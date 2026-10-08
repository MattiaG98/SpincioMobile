namespace Spincio.Engine;

/// <summary>House names for some plays, shown to the players. Names only: they never change what is legal or the points.</summary>
public static class Callouts
{
    /// <summary>
    /// Mariana: a jack (fante) takes a 5 and a 3, of any suits (SPEC, glossary). If it empties the table it is still a
    /// sweep worth its point (P6).
    /// </summary>
    public static bool IsMariana(this CardPlayed play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return play.Card.Rank == Rank.Jack
            && play.Captured.Length == 2
            && play.Captured.Any(c => c.Rank == Rank.Five)
            && play.Captured.Any(c => c.Rank == Rank.Three);
    }
}
