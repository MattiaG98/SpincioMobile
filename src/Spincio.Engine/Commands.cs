namespace Spincio.Engine;

public abstract record Command(Seat Seat);

/// <summary>Play a card. <paramref name="Capture"/> is null for a drop; required when the card can capture (P4).</summary>
public sealed record PlayCard(Seat Seat, Card Card, CaptureOption? Capture = null) : Command(Seat);

/// <summary>Declare the hand points (A3).</summary>
public sealed record Declare(Seat Seat) : Command(Seat);

public enum IllegalReason
{
    MatchOver,
    NotYourTurn,
    CardNotInHand,
    MustCapture,
    CaptureNotAllowed,
    InvalidCapture,
    NothingToDeclare,
    AlreadyPlayedThisDeal,
    AlreadyDeclared,
    UnknownCommand,
}

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, IllegalReason? error)
    {
        _value = value;
        Error = error;
    }

    public IllegalReason? Error { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Command rejected: {Error}");

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(IllegalReason error) => new(default, error);

    public override string ToString() => IsSuccess ? $"Ok({_value})" : $"Rejected({Error})";
}
