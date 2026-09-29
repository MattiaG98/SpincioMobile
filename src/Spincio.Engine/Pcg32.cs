namespace Spincio.Engine;

/// <summary>
/// PCG32 (XSH-RR, 64-bit state) by M. E. O'Neill — the reference <c>pcg32_random_r</c>.
/// Stable across runtimes, unlike <see cref="System.Random"/> is not (ADR 0002).
/// Value type: copying it forks the stream; the engine keeps it inside <see cref="MatchState"/>.
/// </summary>
public struct Pcg32 : IEquatable<Pcg32>
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultStream = 0xda3e39cb94b95bdbUL;

    public Pcg32(ulong initState, ulong initSequence)
    {
        State = 0;
        Increment = (initSequence << 1) | 1UL;
        NextUInt();
        State = unchecked(State + initState);
        NextUInt();
    }

    public ulong State { readonly get; private set; }

    public ulong Increment { readonly get; private set; }

    public static Pcg32 FromSeed(ulong seed) => new(seed, DefaultStream);

    public uint NextUInt()
    {
        ulong old = State;
        State = unchecked((old * Multiplier) + Increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rotation = (int)(old >> 59);
        return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
    }

    /// <summary>Uniform integer in [0, bound) without modulo bias (<c>pcg32_boundedrand_r</c>).</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        uint b = (uint)bound;
        uint threshold = unchecked(0u - b) % b;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold)
            {
                return (int)(r % b);
            }
        }
    }

    /// <summary>Fisher-Yates shuffle into a new array.</summary>
    public Card[] Shuffle(IReadOnlyList<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var result = cards.ToArray();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    public readonly bool Equals(Pcg32 other) => State == other.State && Increment == other.Increment;

    public override readonly bool Equals(object? obj) => obj is Pcg32 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(State, Increment);

    public static bool operator ==(Pcg32 left, Pcg32 right) => left.Equals(right);

    public static bool operator !=(Pcg32 left, Pcg32 right) => !left.Equals(right);
}
