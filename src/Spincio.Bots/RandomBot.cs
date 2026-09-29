using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>L0: picks any legal command uniformly at random.</summary>
public sealed class RandomBot : IBot
{
    public string Name => "L0-Random";

    public Command Choose(PlayerView view, BotMemory memory, ref Pcg32 rng)
    {
        var legal = SpincioEngine.LegalCommands(view);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"{view.Seat} has no legal command.");
        }

        return legal[rng.NextInt(legal.Count)];
    }
}
