namespace Spincio.Engine;

public enum Team
{
    A,
    B,
}

/// <summary>A seat at the table (S2): 0..3, counter-clockwise; team A = 0 and 2, team B = 1 and 3.</summary>
public readonly record struct Seat
{
    public const int Count = 4;

    public Seat(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        Index = index;
    }

    public int Index { get; }

    public Team Team => Index % 2 == 0 ? Team.A : Team.B;

    public static IReadOnlyList<Seat> All { get; } = [new(0), new(1), new(2), new(3)];

    /// <summary>The next seat counter-clockwise.</summary>
    public Seat Next() => new((Index + 1) % Count);

    public override string ToString() => $"Seat{Index}";
}

/// <summary>Match points per team.</summary>
public readonly record struct TeamScores(int A, int B)
{
    public static TeamScores Zero => default;

    public int For(Team team) => team == Team.A ? A : B;

    public TeamScores Add(Team team, int points) =>
        team == Team.A ? this with { A = A + points } : this with { B = B + points };

    public TeamScores Add(TeamScores other) => new(A + other.A, B + other.B);
}
