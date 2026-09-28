using System.Text.Json;
using FsCheck.Xunit;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Server.Tests;

/// <summary>Engine types must survive the trip over SignalR unchanged.</summary>
public class ContractsTests
{
    [Fact]
    public void Every_engine_event_type_is_registered_for_serialization()
    {
        var engineEvents = typeof(GameEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract);

        SpincioJson.EventTypes.ShouldBe(engineEvents, ignoreOrder: true);
    }

    /// <summary>Views and events of whole random matches round-trip through JSON.</summary>
    [Property(MaxTest = 10)]
    public void Updates_round_trip(ulong seed)
    {
        var rng = new Pcg32(seed, 5);
        var step = SpincioEngine.NewMatch(seed);
        while (true)
        {
            foreach (var seat in Seat.All)
            {
                var update = new GameUpdate(step.State.Sequence, SpincioEngine.ViewFor(step.State, seat), [.. step.EventsFor(seat)], 12);
                string json = JsonSerializer.Serialize(update, SpincioJson.Options);
                var back = JsonSerializer.Deserialize<GameUpdate>(json, SpincioJson.Options)!;

                JsonSerializer.Serialize(back, SpincioJson.Options).ShouldBe(json);
                back.View.Seat.ShouldBe(seat);
                back.View.Hand.ShouldBe(update.View.Hand);
                back.Events.Select(e => e.GetType()).ShouldBe(update.Events.Select(e => e.GetType()));
            }

            if (step.State.Phase == MatchPhase.MatchOver)
            {
                break;
            }

            var legal = SpincioEngine.LegalCommands(step.State, step.State.ToPlay);
            step = SpincioEngine.Apply(step.State, legal[rng.NextInt(legal.Count)]).Value;
        }
    }

    [Fact]
    public void Commitment_is_deterministic_and_depends_on_seed_and_salt()
    {
        Commitment.Of(1, "a").ShouldBe(Commitment.Of(1, "a"));
        Commitment.Of(1, "a").ShouldNotBe(Commitment.Of(2, "a"));
        Commitment.Of(1, "a").ShouldNotBe(Commitment.Of(1, "b"));
        Commitment.Of(1, "a").Length.ShouldBe(64);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("P9:7D")]
    [InlineData("P1:ZZ")]
    [InlineData("P1:7D>3S>4B")]
    public void Invalid_commands_are_rejected(string text)
    {
        CommandCodec.TryDecode(text, out _).ShouldBeFalse();
    }
}
