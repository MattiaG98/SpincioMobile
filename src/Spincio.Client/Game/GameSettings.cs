using System.Text.Json;
using Microsoft.JSInterop;
using Spincio.Bots;

namespace Spincio.Client.Game;

public enum AnimationSpeed
{
    Slow,
    Normal,
    Fast,
    Off,
}

/// <summary>The player's options (menu "Opzioni"), kept in the browser's localStorage.</summary>
public sealed record GameSettings(BotLevel Difficulty = BotLevel.Greedy, AnimationSpeed Animations = AnimationSpeed.Normal)
{
    /// <summary>Multiplier for the card animations (js/moves.js); 0 = none.</summary>
    public double AnimationFactor => Animations switch
    {
        AnimationSpeed.Slow => 1.5,
        AnimationSpeed.Fast => 0.6,
        AnimationSpeed.Off => 0,
        _ => 1,
    };
}

/// <summary>Loads and saves <see cref="GameSettings"/>; a missing or unreadable value gives the defaults.</summary>
public sealed class SettingsService(IJSRuntime js)
{
    private const string Key = "spincio.settings";

    public GameSettings Current { get; private set; } = new();

    public async Task<GameSettings> LoadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            Current = string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<GameSettings>(json) ?? new();
        }
        catch (Exception e) when (e is JSException or JsonException or InvalidOperationException)
        {
            Current = new();
        }

        await ApplyAsync();
        return Current;
    }

    public async Task SaveAsync(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings;
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(settings));
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }

        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        try
        {
            await js.InvokeVoidAsync("spincioMoves.setSpeed", Current.AnimationFactor);
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }
}
