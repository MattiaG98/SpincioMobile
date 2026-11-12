using System.Numerics;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>What a team's captured cards count for at the end of the round (F1–F7), kept as plain counters.</summary>
internal struct CardTally
{
    public int Cards;
    public int Coins;
    public int Sevens;
    public int Settebelli;
    public int Rebelli;

    /// <summary>Bit r − 1 set for each coin rank r taken (copies do not lengthen the spincio, SP8).</summary>
    public int CoinRanks;

    public static CardTally Of(IEnumerable<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var tally = default(CardTally);
        foreach (var card in cards)
        {
            tally.Add(card);
        }

        return tally;
    }

    public void Add(Card card)
    {
        Cards++;
        if (card.IsCoins)
        {
            Coins++;
            CoinRanks |= 1 << (card.Value - 1);
        }

        if (card.Rank == Rank.Seven)
        {
            Sevens++;
        }

        if (card == RoundScoring.Settebello)
        {
            Settebelli++;
        }
        else if (card == RoundScoring.Rebello)
        {
            Rebelli++;
        }
    }
}

/// <summary>
/// Round points from captured cards: exact when the round is over, and expected while it is still being played,
/// assuming each card not yet taken is equally likely to end up with either team. The expectation is what makes the
/// CPU value a card by what it is worth now: the seventh coin when the coins are already won is worth little, the
/// three of coins when we hold the ace and the two starts a spincio.
/// </summary>
internal static class RoundOutlook
{
    private const int Ranks = 10;
    private static readonly double[][] Cdf = BinomialCdf(80);

    /// <summary>Round points of team A minus team B when the round is over (F1–F6), plus <paramref name="allCoinsValue"/> for F7.</summary>
    public static double Exact(in CardTally a, in CardTally b, GameVariant variant, double allCoinsValue)
    {
        double value = Points(a, b) - Points(b, a);
        if (variant.AllCoinsWins())
        {
            value += a.Coins == Ranks ? allCoinsValue : b.Coins == Ranks ? -allCoinsValue : 0;
        }

        return value;
    }

    /// <summary>Round points team A can expect over team B from the cards taken so far.</summary>
    public static double Expected(in CardTally a, in CardTally b, GameVariant variant, double allCoinsValue)
    {
        int copies = variant.DeckCopies();
        int cards = variant.CardCount(), coins = Ranks * copies, sevens = 4 * copies;
        double value = Majority(a.Cards, b.Cards, cards - a.Cards - b.Cards)
            + Majority(a.Coins, b.Coins, coins - a.Coins - b.Coins)
            + Majority(a.Sevens, b.Sevens, sevens - a.Sevens - b.Sevens)
            + (a.Settebelli - b.Settebelli) + (a.Rebelli - b.Rebelli)
            + Spincio(a.CoinRanks, b.CoinRanks, copies) - Spincio(b.CoinRanks, a.CoinRanks, copies);
        if (variant.AllCoinsWins())
        {
            double allA = b.Coins == 0 ? Math.Pow(0.5, coins - a.Coins) : 0;
            double allB = a.Coins == 0 ? Math.Pow(0.5, coins - b.Coins) : 0;
            value += allCoinsValue * (allA - allB);
        }

        return value;
    }

    /// <summary>+1 if the first team ends with more, −1 if the other does, expected over a fair split of the rest.</summary>
    public static double Majority(int mine, int theirs, int remaining)
    {
        // mine + X > theirs + (remaining − X)  ⇔  X > (theirs − mine + remaining) / 2, with X ~ Binomial(remaining, ½).
        double threshold = (theirs - mine + remaining) / 2.0;
        double more = 1 - CdfAt(remaining, (int)Math.Floor(threshold));
        double less = CdfAt(remaining, (int)Math.Ceiling(threshold) - 1);
        return more - less;
    }

    /// <summary>
    /// Expected spincio points (F6) of a team holding the coin ranks <paramref name="mine"/>: a rank it does not hold
    /// yet comes with probability ½, or never in the Spincio if the other team took it.
    /// </summary>
    public static double Spincio(int mine, int theirs, int copies)
    {
        double atLeast = 1, expected = 0; // P(run ≥ length)
        for (int length = 0; length < Ranks; length++)
        {
            double p = (mine & (1 << length)) != 0 ? 1 : copies == 1 && (theirs & (1 << length)) != 0 ? 0 : 0.5;
            double next = atLeast * p;
            if (length >= RoundScoring.MinSpincioLength)
            {
                expected += length * (atLeast - next);
            }

            atLeast = next;
            if (atLeast == 0)
            {
                return expected;
            }
        }

        return expected + (Ranks * atLeast);
    }

    private static int Points(in CardTally pile, in CardTally other)
    {
        int points = (pile.Cards > other.Cards ? 1 : 0) + (pile.Coins > other.Coins ? 1 : 0) + (pile.Sevens > other.Sevens ? 1 : 0)
            + pile.Settebelli + pile.Rebelli;
        int run = BitOperations.TrailingZeroCount(~pile.CoinRanks);
        return points + (run >= RoundScoring.MinSpincioLength ? run : 0);
    }

    private static double CdfAt(int n, int k) => k < 0 ? 0 : k >= n ? 1 : Cdf[n][k];

    private static double[][] BinomialCdf(int max)
    {
        var cdf = new double[max + 1][];
        var pmf = new double[max + 1];
        pmf[0] = 1;
        for (int n = 0; n <= max; n++)
        {
            if (n > 0)
            {
                for (int k = n; k >= 1; k--)
                {
                    pmf[k] = (pmf[k] + pmf[k - 1]) / 2;
                }

                pmf[0] /= 2;
            }

            cdf[n] = new double[n + 1];
            double sum = 0;
            for (int k = 0; k <= n; k++)
            {
                sum += pmf[k];
                cdf[n][k] = sum;
            }
        }

        return cdf;
    }
}
