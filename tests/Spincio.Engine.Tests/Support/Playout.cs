using System.Collections.Immutable;
using System.Text.Json;
using Spincio.Engine;

namespace Spincio.Engine.Tests.Support;

internal static class Playout
{
    public const int MaxCommands = 50_000;

    /// <summary>Applies a command that must be legal.</summary>
    public static Transition Ok(this MatchState state, Command command)
    {
        var result = SpincioEngine.Apply(state, command);
        result.IsSuccess.ShouldBeTrue($"{command} rejected: {result.Error}");
        return result.Value;
    }

    /// <summary>
    /// Plays a whole match choosing uniformly among legal commands with its own PCG32 stream.
    /// Calls <paramref name="onStep"/> after the start and after every accepted command.
    /// </summary>
    public static IReadOnlyList<Transition> RandomMatch(ulong seed, Action<Transition>? onStep = null, Seat? firstDealer = null)
    {
        var chooser = new Pcg32(seed, 7);
        var start = SpincioEngine.NewMatch(seed, firstDealer);
        var steps = new List<Transition> { start };
        onStep?.Invoke(start);

        var state = start.State;
        while (state.Phase != MatchPhase.MatchOver)
        {
            if (steps.Count > MaxCommands)
            {
                throw new InvalidOperationException($"Seed {seed}: match did not end within {MaxCommands} commands.");
            }

            var legal = SpincioEngine.LegalCommands(state, state.ToPlay);
            legal.ShouldNotBeEmpty();
            var step = state.Ok(legal[chooser.NextInt(legal.Count)]);
            steps.Add(step);
            onStep?.Invoke(step);
            state = step.State;
        }

        return steps;
    }

    /// <summary>Canonical text form of a transition, for structural comparison (ImmutableArray equality is by reference).</summary>
    public static string Snapshot(Transition transition) =>
        JsonSerializer.Serialize(transition.State)
        + "|"
        + string.Join("|", transition.Events.Select(e => e.GetType().Name + JsonSerializer.Serialize(e, e.GetType())));

    public static ImmutableArray<T> EventsOf<T>(this Transition transition) => [.. transition.Events.OfType<T>()];
}
