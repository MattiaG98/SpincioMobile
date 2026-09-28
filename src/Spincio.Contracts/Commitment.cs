using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spincio.Engine;

namespace Spincio.Contracts;

/// <summary>
/// Commit-reveal of the match seed (ADR 0004): the server publishes <see cref="Of"/> before the first card
/// and reveals seed and salt at the end. Anyone can then recompute the hash and replay the match.
/// </summary>
public static class Commitment
{
    public static string Of(ulong seed, string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);
        var bytes = Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture) + ":" + salt);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>
    /// True when the revealed seed matches the commitment and replaying the command log from it
    /// produces exactly the announced final score.
    /// </summary>
    public static bool Verify(string commitment, MatchReveal reveal, TeamScores finalScore)
    {
        ArgumentNullException.ThrowIfNull(reveal);
        if (!string.Equals(commitment, Of(reveal.Seed, reveal.Salt), StringComparison.Ordinal))
        {
            return false;
        }

        var state = SpincioEngine.NewMatch(reveal.Seed).State;
        foreach (var text in reveal.Commands)
        {
            if (!CommandCodec.TryDecode(text, out var command))
            {
                return false;
            }

            var result = SpincioEngine.Apply(state, command!);
            if (!result.IsSuccess)
            {
                return false;
            }

            state = result.Value.State;
        }

        return state.Phase == MatchPhase.MatchOver && state.Score == finalScore;
    }
}
