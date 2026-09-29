namespace Spincio.Engine;

/// <summary>End-of-round tally for one team (F1–F6). Sweeps and declarations are scored live, not here.</summary>
public sealed record TeamTally(
    int CardCount,
    int CoinCount,
    int SevenCount,
    int NapolaLength,
    int CardsPoint,
    int CoinsPoint,
    int RebelloPoint,
    int SettebelloPoint,
    int PrimieraPoint,
    int NapolaPoints)
{
    public int Total => CardsPoint + CoinsPoint + RebelloPoint + SettebelloPoint + PrimieraPoint + NapolaPoints;
}

public sealed record RoundScore(TeamTally A, TeamTally B, Team? AllCoinsTeam)
{
    public TeamTally For(Team team) => team == Team.A ? A : B;

    public TeamScores Totals => new(A.Total, B.Total);
}

public static class RoundScoring
{
    public const int MinNapolaLength = 3;
    public static readonly Card Rebello = new(Rank.King, Suit.Coins);
    public static readonly Card Settebello = new(Rank.Seven, Suit.Coins);

    public static RoundScore Score(IReadOnlyCollection<Card> pileA, IReadOnlyCollection<Card> pileB)
    {
        ArgumentNullException.ThrowIfNull(pileA);
        ArgumentNullException.ThrowIfNull(pileB);

        int cardsA = pileA.Count, cardsB = pileB.Count;
        int coinsA = pileA.Count(c => c.IsCoins), coinsB = pileB.Count(c => c.IsCoins);
        int sevensA = pileA.Count(c => c.Rank == Rank.Seven), sevensB = pileB.Count(c => c.Rank == Rank.Seven);
        int napolaA = NapolaLength(pileA), napolaB = NapolaLength(pileB);

        TeamTally Tally(IReadOnlyCollection<Card> pile, int cards, int otherCards, int coins, int otherCoins, int sevens, int otherSevens, int napola) =>
            new(
                CardCount: cards,
                CoinCount: coins,
                SevenCount: sevens,
                NapolaLength: napola,
                CardsPoint: cards > otherCards ? 1 : 0,
                CoinsPoint: coins > otherCoins ? 1 : 0,
                RebelloPoint: pile.Contains(Rebello) ? 1 : 0,
                SettebelloPoint: pile.Contains(Settebello) ? 1 : 0,
                PrimieraPoint: sevens > otherSevens ? 1 : 0,
                NapolaPoints: napola);

        int coinsInDeck = Enum.GetValues<Rank>().Length;
        Team? allCoins = coinsA == coinsInDeck ? Team.A : coinsB == coinsInDeck ? Team.B : null;

        return new RoundScore(
            Tally(pileA, cardsA, cardsB, coinsA, coinsB, sevensA, sevensB, napolaA),
            Tally(pileB, cardsB, cardsA, coinsB, coinsA, sevensB, sevensA, napolaB),
            allCoins);
    }

    /// <summary>F6: length of the consecutive coins run starting from the ace, or 0 if shorter than 3.</summary>
    public static int NapolaLength(IEnumerable<Card> pile)
    {
        var coinRanks = pile.Where(c => c.IsCoins).Select(c => c.Rank).ToHashSet();
        int length = 0;
        foreach (var rank in Enum.GetValues<Rank>())
        {
            if (!coinRanks.Contains(rank))
            {
                break;
            }

            length++;
        }

        return length >= MinNapolaLength ? length : 0;
    }
}

public enum WinReason
{
    Score,
    AllCoins,
}

public enum MatchOutcomeKind
{
    Continue,
    Win,
    Tiebreak,
}

public readonly record struct MatchOutcome(MatchOutcomeKind Kind, Team? Winner = null, WinReason? Reason = null);

/// <summary>End-of-match check E1, E2, F7 — evaluated only at the end of a round.</summary>
public static class MatchRules
{
    public const int TargetScore = 31;

    public static MatchOutcome Evaluate(TeamScores score, Team? allCoinsTeam)
    {
        if (allCoinsTeam is { } coins)
        {
            return new(MatchOutcomeKind.Win, coins, WinReason.AllCoins);
        }

        if (Math.Max(score.A, score.B) < TargetScore)
        {
            return new(MatchOutcomeKind.Continue);
        }

        if (score.A == score.B)
        {
            return new(MatchOutcomeKind.Tiebreak);
        }

        return new(MatchOutcomeKind.Win, score.A > score.B ? Team.A : Team.B, WinReason.Score);
    }
}
