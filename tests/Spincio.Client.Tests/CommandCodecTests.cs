using FsCheck.Xunit;
using Spincio.Client.Game;
using Spincio.Engine;

namespace Spincio.Client.Tests;

public class CommandCodecTests
{
    [Theory]
    [InlineData("D2")]
    [InlineData("P1:7D")]
    [InlineData("P3:KB>7C+3S")] // captured cards in canonical order (suit, then rank)
    public void Round_trips(string text)
    {
        CommandCodec.Encode(CommandCodec.Decode(text)).ShouldBe(text);
    }

    [Property(MaxTest = 30)]
    public void Every_legal_command_of_a_match_round_trips(ulong seed)
    {
        var rng = new Pcg32(seed, 3);
        var state = SpincioEngine.NewMatch(seed).State;
        while (state.Phase != MatchPhase.MatchOver)
        {
            var legal = SpincioEngine.LegalCommands(state, state.ToPlay);
            foreach (var command in legal)
            {
                CommandCodec.Decode(CommandCodec.Encode(command)).ShouldBe(command);
            }

            state = SpincioEngine.Apply(state, legal[rng.NextInt(legal.Count)]).Value.State;
        }
    }
}
