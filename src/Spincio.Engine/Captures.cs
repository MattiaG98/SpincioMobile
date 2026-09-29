using System.Collections.Immutable;

namespace Spincio.Engine;

/// <summary>A set of table cards taken by a play. Equality is set equality (order-insensitive).</summary>
public sealed class CaptureOption : IEquatable<CaptureOption>
{
    public CaptureOption(IEnumerable<Card> cards)
    {
        Cards = [.. cards.Order()];
    }

    /// <summary>Captured table cards in canonical order.</summary>
    public ImmutableArray<Card> Cards { get; }

    public bool Equals(CaptureOption? other) => other is not null && Cards.SequenceEqual(other.Cards);

    public override bool Equals(object? obj) => Equals(obj as CaptureOption);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var card in Cards)
        {
            hash.Add(card);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => "{" + string.Join(",", Cards) + "}";
}

/// <summary>Capture rules P1–P5 (SPEC §5).</summary>
public static class Captures
{
    /// <summary>
    /// Every capture the played card can make. Empty means the card is dropped.
    /// An equal-value card forbids sums (P2); otherwise every subset of ≥2 cards summing to the value (P1, P3).
    /// </summary>
    public static ImmutableArray<CaptureOption> Options(Card played, IReadOnlyList<Card> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        int target = played.Value;

        var equal = table.Where(t => t.Value == target).Order().Select(t => new CaptureOption([t])).ToImmutableArray();
        if (!equal.IsEmpty)
        {
            return equal;
        }

        var sorted = table.OrderBy(c => c.Value).ThenBy(c => c).ToArray();
        var options = ImmutableArray.CreateBuilder<CaptureOption>();
        var chosen = new List<Card>(sorted.Length);
        CollectSums(sorted, 0, 0, target, chosen, options);
        return options.ToImmutable();
    }

    private static void CollectSums(
        Card[] sorted, int start, int sum, int target, List<Card> chosen, ImmutableArray<CaptureOption>.Builder options)
    {
        for (int i = start; i < sorted.Length; i++)
        {
            int next = sum + sorted[i].Value;
            if (next > target)
            {
                return; // ascending values: nothing further fits
            }

            chosen.Add(sorted[i]);
            if (next == target)
            {
                if (chosen.Count >= 2)
                {
                    options.Add(new CaptureOption(chosen));
                }
            }
            else
            {
                CollectSums(sorted, i + 1, next, target, chosen, options);
            }

            chosen.RemoveAt(chosen.Count - 1);
        }
    }
}
