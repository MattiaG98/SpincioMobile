using System.Globalization;
using Spincio.Engine;

namespace Spincio.Contracts;

/// <summary>
/// Compact text form of commands, used for the saved command log (ADR 0003) and on the wire (ADR 0004):
/// <c>D0</c> = seat 0 declares; <c>P1:7D</c> = seat 1 drops 7D; <c>P1:7D&gt;3S+4B</c> = seat 1 takes 3S and 4B with 7D.
/// </summary>
public static class CommandCodec
{
    public static string Encode(Command command) => command switch
    {
        Declare d => string.Create(CultureInfo.InvariantCulture, $"D{d.Seat.Index}"),
        PlayCard { Capture: null } p => string.Create(CultureInfo.InvariantCulture, $"P{p.Seat.Index}:{p.Card}"),
        PlayCard p => string.Create(CultureInfo.InvariantCulture, $"P{p.Seat.Index}:{p.Card}>{string.Join('+', p.Capture!.Cards)}"),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };

    /// <exception cref="FormatException">The text is not a valid command.</exception>
    public static Command Decode(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            throw new FormatException($"Invalid command '{text}'.");
        }

        int seatIndex = text[1] - '0';
        if (seatIndex is < 0 or >= Seat.Count)
        {
            throw new FormatException($"Invalid command '{text}'.");
        }

        var seat = new Seat(seatIndex);
        if (text[0] == 'D' && text.Length == 2)
        {
            return new Declare(seat);
        }

        if (text[0] != 'P' || text.Length < 5 || text[2] != ':')
        {
            throw new FormatException($"Invalid command '{text}'.");
        }

        var parts = text[3..].Split('>');
        if (parts.Length > 2)
        {
            throw new FormatException($"Invalid command '{text}'.");
        }

        var card = Card.Parse(parts[0]);
        return parts.Length == 1
            ? new PlayCard(seat, card)
            : new PlayCard(seat, card, new CaptureOption(parts[1].Split('+').Select(Card.Parse)));
    }

    public static bool TryDecode(string? text, out Command? command)
    {
        try
        {
            command = Decode(text ?? "");
            return true;
        }
        catch (FormatException)
        {
            command = null;
            return false;
        }
    }
}
