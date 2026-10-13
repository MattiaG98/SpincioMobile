using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spincio.Contracts;

namespace Spincio.Client.Game;

/// <summary>Who this device is online: a random id kept on the device, and the player's name (ADR 0015).</summary>
public sealed record PlayerProfile(string Id, string Name)
{
    [JsonIgnore]
    public bool HasName => !string.IsNullOrWhiteSpace(Name);
}

/// <summary>Keeps the <see cref="PlayerProfile"/> in localStorage; the id is created on first use.</summary>
public sealed class ProfileService(IJSRuntime js)
{
    private const string Key = "spincio.player";
    private PlayerProfile? _profile;
    private Task? _loading;

    /// <summary>The profile, read once (the invitation bar and the online page both ask at start-up).</summary>
    public async Task<PlayerProfile> LoadAsync()
    {
        await (_loading ??= ReadAsync());
        return _profile!;
    }

    public async Task<PlayerProfile> SetNameAsync(string name)
    {
        var profile = await LoadAsync() with { Name = (name ?? "").Trim() };
        await SaveAsync(profile);
        return profile;
    }

    private async Task ReadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            _profile = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<PlayerProfile>(json);
        }
        catch (Exception e) when (e is JSException or JsonException or InvalidOperationException)
        {
        }

        if (_profile is null || string.IsNullOrEmpty(_profile.Id) || !_profile.Id.All(char.IsAsciiLetterOrDigit))
        {
            _profile = new PlayerProfile(Guid.NewGuid().ToString("N"), _profile?.Name ?? "");
            await SaveAsync(_profile);
        }
    }

    private async Task SaveAsync(PlayerProfile profile)
    {
        _profile = profile;
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(profile));
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }
}

public enum LobbyState
{
    /// <summary>No server or no name yet.</summary>
    Off,
    Connecting,
    Online,

    /// <summary>The server does not answer (e.g. a temporary server switched off); it is tried again every so often.</summary>
    Unreachable,
}

/// <summary>What happened to an invitation this device sent, as shown next to the invited player.</summary>
public static class InviteStatus
{
    public const string Sent = "Invitato…";
    public const string Accepted = "Ha accettato";
    public const string Declined = "Ha rifiutato";
}

/// <summary>Who is online and invitations, as the pages and the invitation bar see them (ADR 0015).</summary>
public interface ILobby
{
    event Action? Changed;

    LobbyState State { get; }

    /// <summary>This device's player id (to leave itself out of the list).</summary>
    string? MyId { get; }

    IReadOnlyList<OnlinePlayer> Players { get; }

    /// <summary>The latest invitation received and not answered yet.</summary>
    Invitation? Pending { get; }

    /// <summary>What happened to the invitations this device sent, by invited player id.</summary>
    IReadOnlyDictionary<string, string> Sent { get; }

    /// <summary>Connects to <paramref name="server"/> as <paramref name="profile"/> (again if either changed).</summary>
    Task StartAsync(Uri server, PlayerProfile profile);

    /// <summary>Disconnects (e.g. the server was forgotten).</summary>
    Task StopAsync();

    Task<CommandResult> InviteAsync(OnlinePlayer player, string seatToken);

    Task AnswerAsync(Invitation invitation, bool accepted);
}

/// <summary>
/// The lobby connection (SignalR, <see cref="HubPaths.Lobby"/>). It reconnects by itself after a drop and, while the
/// server does not answer, tries again every <see cref="RetryEvery"/>.
/// </summary>
public sealed class LobbyClient(Func<HttpMessageHandler>? handler = null) : ILobby, IAsyncDisposable
{
    public static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(20);

    private readonly Dictionary<string, string> _sent = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _switching = new(1, 1); // one start or stop at a time
    private HubConnection? _hub;
    private Uri? _server;
    private PlayerProfile? _profile;
    private CancellationTokenSource? _retry;
    private int _lists;

    public event Action? Changed;

    public LobbyState State { get; private set; }

    public string? MyId => _profile?.Id;

    public IReadOnlyList<OnlinePlayer> Players { get; private set; } = [];

    public Invitation? Pending { get; private set; }

    public IReadOnlyDictionary<string, string> Sent => _sent;

    public async Task StartAsync(Uri server, PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.HasName)
        {
            return;
        }

        await _switching.WaitAsync();
        try
        {
            if (_hub is not null && server == _server && profile == _profile && State is LobbyState.Online or LobbyState.Connecting)
            {
                return;
            }

            await StopCoreAsync();
            Connect(server, profile);
        }
        finally
        {
            _switching.Release();
        }
    }

    public async Task StopAsync()
    {
        await _switching.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _switching.Release();
        }

        Changed?.Invoke();
    }

    private void Connect(Uri server, PlayerProfile profile)
    {
        _server = server;
        _profile = profile;
        var hub = _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server, HubPaths.Lobby), options =>
            {
                if (handler is not null)
                {
                    options.HttpMessageHandlerFactory = _ => handler();
                    options.Transports = HttpTransportType.LongPolling;
                }
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(o => SpincioJson.Configure(o.PayloadSerializerOptions))
            .Build();
        var retry = _retry = new CancellationTokenSource();
        hub.On<IReadOnlyList<OnlinePlayer>>(nameof(ILobbyClient.PlayersChanged), players => Set(() =>
        {
            _lists++;
            Players = players;
        }));
        hub.On<Invitation>(nameof(ILobbyClient.Invited), invitation => Set(() => Pending = invitation));
        hub.On<string, string, string, bool>(nameof(ILobbyClient.InvitationAnswered), (_, byId, _, accepted) =>
            Set(() => _sent[byId] = accepted ? InviteStatus.Accepted : InviteStatus.Declined));
        hub.Reconnecting += _ => SetAsync(() => State = LobbyState.Connecting);
        hub.Reconnected += async _ => await HelloAsync();
        hub.Closed += error =>
        {
            // Closed on purpose (stop, another server or name), or reconnecting gave up: then keep trying slowly.
            if (!retry.IsCancellationRequested)
            {
                Set(() => State = LobbyState.Unreachable);
                _ = KeepTryingAsync(hub, RetryEvery, retry.Token);
            }

            return Task.CompletedTask;
        };

        _ = KeepTryingAsync(hub, TimeSpan.Zero, retry.Token);
    }

    public async Task<CommandResult> InviteAsync(OnlinePlayer player, string seatToken)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (_hub is not { State: HubConnectionState.Connected } hub)
        {
            return CommandResult.Fail("Non sei collegato al server.");
        }

        var result = await hub.InvokeAsync<CommandResult>(nameof(ILobbyHub.Invite), player.Id, seatToken);
        Set(() => _sent[player.Id] = result.Ok ? InviteStatus.Sent : result.Error ?? "Invito non riuscito");
        return result;
    }

    public async Task AnswerAsync(Invitation invitation, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        Set(() => Pending = Pending?.Id == invitation.Id ? null : Pending);
        if (_hub is { State: HubConnectionState.Connected } hub)
        {
            await hub.InvokeAsync(nameof(ILobbyHub.Answer), invitation.Id, accepted);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopCoreAsync();
        _switching.Dispose();
    }

    private async Task KeepTryingAsync(HubConnection hub, TimeSpan wait, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(wait, stop);
                Set(() => State = LobbyState.Connecting);
                if (hub.State == HubConnectionState.Disconnected)
                {
                    await hub.StartAsync(stop);
                }

                await HelloAsync();
                return;
            }
            catch (Exception)
            {
                // Whatever went wrong (server off, network, stopped on purpose), it is tried again later.
                if (stop.IsCancellationRequested)
                {
                    return;
                }

                Set(() => State = LobbyState.Unreachable);
            }

            wait = RetryEvery;
        }
    }

    private async Task HelloAsync()
    {
        if (_hub is not { } hub || _profile is not { } profile)
        {
            return;
        }

        // The server also sends the list to everyone, this device included: if that arrived first, it is the newer one.
        int lists = _lists;
        var players = await hub.InvokeAsync<IReadOnlyList<OnlinePlayer>>(nameof(ILobbyHub.Hello), profile.Id, profile.Name);
        Set(() =>
        {
            if (lists == _lists)
            {
                Players = players;
            }

            State = LobbyState.Online;
        });
    }

    private async Task StopCoreAsync()
    {
        if (_retry is not null)
        {
            await _retry.CancelAsync();
            _retry.Dispose();
            _retry = null;
        }

        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }

        Players = [];
        State = LobbyState.Off;
    }

    private void Set(Action change)
    {
        change();
        Changed?.Invoke();
    }

    private Task SetAsync(Action change)
    {
        Set(change);
        return Task.CompletedTask;
    }
}
