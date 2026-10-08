using System.Text.Json;
using Bunit;
using Spincio.Client.Game;

namespace Spincio.Client.Tests;

/// <summary>Statistics storage: what is read back from localStorage, and what is ignored.</summary>
public class StatsServiceTests : BunitContext
{
    private StatsService NewService(string? stored)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("localStorage.getItem", "spincio.stats").SetResult(stored);
        var session = new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask);
        return new StatsService(JSInterop.JSRuntime, new GameHost(session));
    }

    [Fact]
    public async Task Reads_back_saved_statistics()
    {
        var saved = new PlayerStats { Normal = new ModeStats(3, 2), MySweeps = 5 };

        (await NewService(JsonSerializer.Serialize(saved)).LoadAsync()).ShouldBe(saved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("""{"Version":99,"MySweeps":4}""")]
    public async Task Missing_unreadable_or_other_version_statistics_start_empty(string? stored)
    {
        (await NewService(stored).LoadAsync()).ShouldBe(new PlayerStats());
    }

    [Fact]
    public async Task Abandon_is_saved_on_top_of_what_was_loaded()
    {
        var service = NewService(JsonSerializer.Serialize(new PlayerStats { Abandoned = 2, CurrentStreak = 3, BestStreak = 3 }));

        await service.RecordAbandonAsync();

        var stats = await service.LoadAsync();
        stats.Abandoned.ShouldBe(3);
        stats.CurrentStreak.ShouldBe(0);
        stats.BestStreak.ShouldBe(3);
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments[1].ShouldBe(JsonSerializer.Serialize(stats));
    }
}
