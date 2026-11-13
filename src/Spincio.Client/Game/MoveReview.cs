using System.Collections.Immutable;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// A CPU move the player can report (ADR 0020): what it did and the position it did it in.
/// <paramref name="CommandCount"/> is the length of the match log up to and including this move.
/// </summary>
/// <param name="HandBefore">The CPU's hand before the move, once that deal is over (all its cards were played in the open by then); null before.</param>
public sealed record ReviewableMove(
    int CommandCount,
    CardPlayed Play,
    int MatchNumber,
    int RoundNumber,
    int DealNumber,
    ImmutableArray<Card> TableBefore,
    ImmutableArray<Card>? HandBefore);

public static class MoveReview
{
    /// <summary>The moves of <paramref name="cpu"/> in the match so far, newest first, replayed from seed and commands (ADR 0003).</summary>
    public static IReadOnlyList<ReviewableMove> MovesOf(Seat cpu, ulong seed, GameVariant variant, IReadOnlyList<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var state = SpincioEngine.NewMatch(seed, variant: variant).State;
        var found = new List<(ReviewableMove Move, MatchState Before)>();
        for (int i = 0; i < commands.Count; i++)
        {
            var command = CommandCodec.Decode(commands[i]);
            var step = SpincioEngine.Apply(state, command).Value;
            if (command is PlayCard play && play.Seat == cpu)
            {
                var played = step.Events.OfType<CardPlayed>().Single();
                found.Add((new ReviewableMove(i + 1, played, state.MatchNumber, state.RoundNumber, state.DealNumber, state.Table, null), state));
            }

            state = step.State;
        }

        // A deal is over once the match has moved past it: then the CPU's hand of that deal is no secret any more.
        bool Over(MatchState before) => state.Phase == MatchPhase.MatchOver
            || (before.MatchNumber, before.RoundNumber, before.DealNumber) != (state.MatchNumber, state.RoundNumber, state.DealNumber);

        return [.. found.Select(f => Over(f.Before) ? f.Move with { HandBefore = f.Before.HandOf(cpu) } : f.Move).Reverse()];
    }

    /// <summary>"Ha preso il 3 di spade + il 4 di bastoni con il 7 di denari" / "Ha calato il re di bastoni".</summary>
    public static string Describe(CardPlayed play)
    {
        ArgumentNullException.ThrowIfNull(play);
        if (play.Captured.IsEmpty)
        {
            return $"Ha calato {GameText.CardName(play.Card)}";
        }

        return $"Ha preso {GameText.Cards(play.Captured)} con {GameText.CardName(play.Card)}" + (play.IsSweep ? " · spazzino" : "");
    }

    /// <summary>"Smazzata 2 · mano 3" (with the tiebreak number in a tiebreak).</summary>
    public static string When(ReviewableMove move)
    {
        ArgumentNullException.ThrowIfNull(move);
        string tiebreak = move.MatchNumber > 0 ? $"Spareggio {move.MatchNumber} · " : "";
        return $"{tiebreak}Smazzata {move.RoundNumber} · mano {move.DealNumber}";
    }
}
