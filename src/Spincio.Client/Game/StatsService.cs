using System.Text.Json;
using Microsoft.JSInterop;
using Spincio.Bots;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// Keeps <see cref="PlayerStats"/> in the browser's localStorage and updates them from the live events of the
/// current match, offline or online (<see cref="IGameSession.LiveEvents"/>: a resumed match is not counted twice).
/// </summary>
public sealed class StatsService
{
    private const string Key = "spincio.stats";
    private readonly IJSRuntime _js;
    private readonly GameHost _host;
    private IGameSession? _watched;
    private Task<PlayerStats>? _loading;

    public StatsService(IJSRuntime js, GameHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _js = js;
        _host = host;
        _host.CurrentChanged += Watch;
        Watch();
    }

    public event Action? Changed;

    public Task<PlayerStats> LoadAsync() => _loading ??= ReadAsync();

    /// <summary>The current match was left before its end (abandoned, or replaced by a new one).</summary>
    public Task RecordAbandonAsync() => UpdateAsync(StatsTracker.Abandon);

    public Task<PlayerStats> ResetAsync() => UpdateAsync(_ => new PlayerStats());

    private void Watch()
    {
        if (_watched is not null)
        {
            _watched.LiveEvents -= OnLiveEvents;
        }

        _watched = _host.Current;
        _watched.LiveEvents += OnLiveEvents;
    }

    private async void OnLiveEvents(IReadOnlyList<GameEvent> events)
    {
        var session = _host.Current;
        var mode = session switch
        {
            LocalGameSession { Difficulty: BotLevel.Pimc } => StatsMode.Hard,
            LocalGameSession => StatsMode.Normal,
            _ => StatsMode.Online,
        };

        try
        {
            await UpdateAsync(stats => StatsTracker.Apply(stats, events, session.Me, mode));
        }
        catch (Exception e) when (e is JSException or InvalidOperationException or TaskCanceledException)
        {
            // Statistics must never break the game.
        }
    }

    // Updates are chained: each one starts from the result of the previous one, even while the first load is pending.
    private Task<PlayerStats> UpdateAsync(Func<PlayerStats, PlayerStats> change)
    {
        var next = ChangeAsync(LoadAsync(), change);
        _loading = next;
        return next;
    }

    private async Task<PlayerStats> ChangeAsync(Task<PlayerStats> previous, Func<PlayerStats, PlayerStats> change)
    {
        var before = await previous;
        var after = change(before);
        if (after != before)
        {
            await SaveAsync(after);
        }

        return after;
    }

    private async Task<PlayerStats> ReadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            var stats = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<PlayerStats>(json);
            return stats is { Version: PlayerStats.CurrentVersion } ? stats : new PlayerStats();
        }
        catch (Exception e) when (e is JSException or JsonException or InvalidOperationException)
        {
            return new PlayerStats();
        }
    }

    private async Task SaveAsync(PlayerStats stats)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(stats));
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }

        Changed?.Invoke();
    }
}
