using System.Text.Json;
using Microsoft.JSInterop;

namespace Spincio.Client.Game;

/// <summary>A match persisted as seed + command log (ADR 0003), tagged with the rules version.</summary>
public sealed record SavedGame(string RulesVersion, ulong Seed, IReadOnlyList<string> Commands)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static SavedGame? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SavedGame>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public interface ISavedGameStore
{
    Task<string?> LoadAsync();

    Task SaveAsync(string json);

    Task ClearAsync();
}

/// <summary>Browser localStorage. May be wiped by the OS on iOS: accepted for the MVP (ADR 0001).</summary>
public sealed class LocalStorageGameStore(IJSRuntime js) : ISavedGameStore
{
    private const string Key = "spincio.savedGame";

    public async Task<string?> LoadAsync() => await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async Task SaveAsync(string json) => await js.InvokeVoidAsync("localStorage.setItem", Key, json);

    public async Task ClearAsync() => await js.InvokeVoidAsync("localStorage.removeItem", Key);
}

public sealed class InMemoryGameStore : ISavedGameStore
{
    public string? Json { get; private set; }

    public Task<string?> LoadAsync() => Task.FromResult(Json);

    public Task SaveAsync(string json)
    {
        Json = json;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Json = null;
        return Task.CompletedTask;
    }
}
