using System.Collections.Immutable;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>
/// Remembers the secret seat token, so a player can come back after closing the page, and the match's latest
/// restore point, so the match can come back even if the server restarted (ADR 0017).
/// </summary>
public interface IOnlineSeatStore
{
    Task<string?> LoadAsync();

    /// <summary>A new seat: also forgets the previous match's restore point.</summary>
    Task SaveAsync(string token);

    Task ClearAsync();

    Task<string?> LoadCheckpointAsync();

    Task SaveCheckpointAsync(string checkpoint);
}

public sealed class LocalStorageOnlineSeatStore(IJSRuntime js) : IOnlineSeatStore
{
    private const string Key = "spincio.onlineSeat";
    private const string CheckpointKey = "spincio.onlineCheckpoint";

    public async Task<string?> LoadAsync() => await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async Task SaveAsync(string token)
    {
        await js.InvokeVoidAsync("localStorage.setItem", Key, token);
        await js.InvokeVoidAsync("localStorage.removeItem", CheckpointKey);
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
        await js.InvokeVoidAsync("localStorage.removeItem", CheckpointKey);
    }

    public async Task<string?> LoadCheckpointAsync() => await js.InvokeAsync<string?>("localStorage.getItem", CheckpointKey);

    public async Task SaveCheckpointAsync(string checkpoint) => await js.InvokeVoidAsync("localStorage.setItem", CheckpointKey, checkpoint);
}

public sealed class InMemoryOnlineSeatStore : IOnlineSeatStore
{
    public string? Token { get; private set; }

    public string? Checkpoint { get; private set; }

    public Task<string?> LoadAsync() => Task.FromResult(Token);

    public Task SaveAsync(string token)
    {
        Token = token;
        Checkpoint = null;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Token = null;
        Checkpoint = null;
        return Task.CompletedTask;
    }

    public Task<string?> LoadCheckpointAsync() => Task.FromResult(Checkpoint);

    public Task SaveCheckpointAsync(string checkpoint)
    {
        Checkpoint = checkpoint;
        return Task.CompletedTask;
    }
}

/// <summary>
/// An online match through the authoritative server (ADR 0004). It only ever holds this player's
/// <see cref="PlayerView"/>: hidden information never reaches the browser.
/// </summary>
public sealed class RemoteGameSession : IGameSession, IAsyncDisposable
{
    public const int FeedLength = 60;

    private readonly HubConnection _hub;
    private readonly IOnlineSeatStore _store;
    private readonly SemaphoreSlim _updates = new(1, 1); // one update at a time, animations included
    private bool _animating;
    private ImmutableList<string> _feed = [];
    private PlayerView? _view;
    private int _sequence;
    private string? _token;
    private string? _checkpoint;
    private CancellationTokenSource _life = new(); // cancelled when leaving or disposing: no more reconnecting

    /// <param name="handler">Test hook: routes the connection through an in-memory server (long polling).</param>
    public RemoteGameSession(Uri serverUrl, IOnlineSeatStore store, Func<HttpMessageHandler>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(serverUrl);
        _store = store;
        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(serverUrl, HubPaths.Game), options =>
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

        _hub.On<RoomInfo>(nameof(IGameClient.RoomChanged), OnRoomChanged);
        _hub.On<GameUpdate>(nameof(IGameClient.Updated), OnUpdatedAsync);
        _hub.On<MatchReveal>(nameof(IGameClient.Revealed), OnRevealed);
        _hub.Reconnected += async _ => await RejoinAsync();
        _hub.Closed += _ => KeepReconnectingAsync();
    }

    /// <summary>
    /// How long to keep trying after the automatic reconnection gave up (e.g. the server restarting, ADR 0017).
    /// </summary>
    public static readonly TimeSpan ReconnectFor = TimeSpan.FromMinutes(3);

    public static readonly TimeSpan ReconnectEvery = TimeSpan.FromSeconds(5);

    public event Action? Changed;

    public event Action<IReadOnlyList<GameEvent>>? LiveEvents;

    public RoomInfo? Room { get; private set; }

    public bool IsStarted => _view is not null;

    public Seat Me { get; private set; }

    public PlayerView View => _view ?? throw new InvalidOperationException("The match has not started.");

    public IReadOnlyList<Command> LegalCommands => _view is null || _animating ? [] : SpincioEngine.LegalCommands(_view);

    public IReadOnlyList<string> Feed => _feed;

    public RoundScored? PendingSummary { get; private set; }

    public MatchEnded? Result { get; private set; }

    public string? Error { get; private set; }

    public bool IsWaiting => _animating || (_view is { Phase: MatchPhase.AwaitingPlay } view && !view.IsMyTurn);

    public int BannerCount { get; private set; }

    public string? BannerText { get; private set; }

    public IMoveAnimator? Animator { get; set; }

    /// <summary>Seconds left for this player's move, as of the last update (0 when it is not our turn).</summary>
    public int TurnSecondsLeft { get; private set; }

    /// <summary>Null until the match ends; then whether the revealed seed matched the commitment and the replay.</summary>
    public bool? Verified { get; private set; }

    public string Footer
    {
        get
        {
            var room = Room is null ? "Online" : $"{GameText.ModeName(Room.Variant)} · stanza {Room.Code}";
            var fairness = Verified switch
            {
                true => " · partita verificata ✓",
                false => " · verifica NON riuscita ✗",
                null when Room?.Commitment is { } c => $" · impegno {c[..8]}",
                _ => "",
            };
            return room + fairness;
        }
    }

    public bool CanRestart => false;

    /// <summary>Online seats are people (or a CPU standing in): no portrait, just the name's initial.</summary>
    public string? SeatAvatar(Seat seat) => null;

    public string SeatName(Seat seat)
    {
        if (seat == Me)
        {
            return "Tu";
        }

        var info = Room?.Seats.FirstOrDefault(s => s.Seat == seat.Index);
        return info switch
        {
            null => GameText.SeatName(seat, Me),
            { IsBot: true, Name: "CPU" } => "CPU",
            { IsBot: true } => $"{info.Name} (CPU)",
            { Connected: false } => $"{info.Name} (disconnesso)",
            _ => info.Name,
        };
    }

    public async Task<JoinResult> CreateRoomAsync(string playerName, GameVariant variant = GameVariant.Spincio)
    {
        await ConnectAsync();
        var joined = variant == GameVariant.Spincio
            ? await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), playerName) // any server version
            : await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoomFor), playerName, variant);
        return await AfterJoinAsync(joined);
    }

    public async Task<JoinResult> JoinRoomAsync(string roomCode, string playerName)
    {
        await ConnectAsync();
        return await AfterJoinAsync(await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), roomCode, playerName));
    }

    /// <summary>Accepts an invitation: joins with the inviter (same team) or against (ADR 0015).</summary>
    public async Task<JoinResult> JoinRoomAsAsync(string roomCode, string playerName, int inviterSeat, bool sameTeam)
    {
        await ConnectAsync();
        return await AfterJoinAsync(await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoomAs), roomCode, playerName, inviterSeat, sameTeam));
    }

    /// <summary>The secret of this player's seat, which proves who is inviting to the room (ADR 0015).</summary>
    public string? SeatToken => _token;

    /// <summary>Comes back to the match remembered in the seat store, if any.</summary>
    public async Task<bool> TryRejoinAsync()
    {
        _token = await _store.LoadAsync();
        _checkpoint = await _store.LoadCheckpointAsync();
        if (string.IsNullOrEmpty(_token))
        {
            return false;
        }

        await ConnectAsync();
        var result = await RejoinAsync();
        if (result is not { Ok: true })
        {
            await _store.ClearAsync();
            _token = null;
            return false;
        }

        return true;
    }

    public async Task<CommandResult> StartMatchAsync()
    {
        var result = await _hub.InvokeAsync<CommandResult>(nameof(IGameHub.Start), _token);
        Error = result.Ok ? null : result.Error;
        Changed?.Invoke();
        return result;
    }

    public async Task PlayAsync(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Seat != Me || _token is null || _animating)
        {
            return;
        }

        var result = await _hub.InvokeAsync<CommandResult>(nameof(IGameHub.Play), _token, _sequence, CommandCodec.Encode(command));
        Error = result.Ok ? null : result.Error;
        Changed?.Invoke();
    }

    public Task AcknowledgeSummaryAsync()
    {
        PendingSummary = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>Leaves the room or match; the session can then create or join a new one (e.g. accept an invitation).</summary>
    public async Task LeaveAsync()
    {
        await _store.ClearAsync();
        _token = null;
        _checkpoint = null;
        await _life.CancelAsync();
        _life.Dispose();
        _life = new CancellationTokenSource();
        await _hub.StopAsync();
        await _updates.WaitAsync();
        try
        {
            Room = null;
            _view = null;
            _sequence = 0;
            _feed = [];
            PendingSummary = null;
            Result = null;
            Verified = null;
            Error = null;
            TurnSecondsLeft = 0;
        }
        finally
        {
            _updates.Release();
        }

        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await _life.CancelAsync();
        await _hub.DisposeAsync();
        _life.Dispose();
        _updates.Dispose();
    }

    private async Task ConnectAsync()
    {
        if (_hub.State == HubConnectionState.Disconnected)
        {
            await _hub.StartAsync();
        }
    }

    private async Task<JoinResult?> RejoinAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            return null;
        }

        var result = await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.Rejoin), _token);
        if (!result.Ok && _checkpoint is { } checkpoint)
        {
            // The server no longer knows the match (it restarted): bring it back from the restore point (ADR 0017).
            try
            {
                result = await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.Resume), _token, checkpoint);
            }
            catch (HubException)
            {
                // A server older than 1.8.0 has no Resume: the match is lost, as before.
            }
        }

        if (result.Ok)
        {
            Me = new Seat(result.Seat);
        }

        return result;
    }

    /// <summary>
    /// After the automatic reconnection gave up, keeps trying for <see cref="ReconnectFor"/> while a match is on:
    /// a restarting server takes up to a minute to answer again (ADR 0016, ADR 0017).
    /// </summary>
    private async Task KeepReconnectingAsync()
    {
        CancellationToken stop;
        try
        {
            stop = _life.Token;
        }
        catch (ObjectDisposedException)
        {
            return; // disposed
        }

        var until = DateTimeOffset.UtcNow + ReconnectFor;
        while (!stop.IsCancellationRequested && _token is not null && Result is null && DateTimeOffset.UtcNow < until)
        {
            try
            {
                await Task.Delay(ReconnectEvery, stop);
                if (_hub.State == HubConnectionState.Disconnected)
                {
                    await _hub.StartAsync(stop);
                }

                if (await RejoinAsync() is { Ok: true })
                {
                    return;
                }
            }
            catch (Exception)
            {
                // Not answering yet (or leaving): try again unless stopped.
                if (stop.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task<JoinResult> AfterJoinAsync(JoinResult result)
    {
        if (result is { Ok: true, Token: { } token })
        {
            _token = token;
            _checkpoint = null;
            Me = new Seat(result.Seat);
            await _store.SaveAsync(token);
            Error = null;
        }
        else
        {
            Error = result.Error;
        }

        Changed?.Invoke();
        return result;
    }

    private async Task KeepCheckpointAsync(string? checkpoint)
    {
        if (checkpoint is null || checkpoint == _checkpoint)
        {
            return;
        }

        _checkpoint = checkpoint;
        try
        {
            await _store.SaveCheckpointAsync(checkpoint);
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
            // Not saved on the device: it still works while the app stays open.
        }
    }

    private void OnRoomChanged(RoomInfo room)
    {
        Room = room;
        Changed?.Invoke();
    }

    private async Task OnUpdatedAsync(GameUpdate update)
    {
        await _updates.WaitAsync();
        try
        {
            // The board still shows the previous view: play the moves on it first, then show the new view.
            bool animated = false;
            if (Animator is { } animator && _view is not null)
            {
                _animating = true;
                try
                {
                    Changed?.Invoke();
                    animated = await animator.AnimateMoveAsync(update.Events, Me);
                }
                finally
                {
                    _animating = false;
                }
            }

            Apply(update);
            LiveEvents?.Invoke(update.Events);
            await KeepCheckpointAsync(update.Checkpoint);
            if (animated && Animator is { } settle)
            {
                await settle.SettleAsync();
            }
        }
        finally
        {
            _updates.Release();
        }
    }

    private void Apply(GameUpdate update)
    {
        _sequence = update.Sequence;
        _view = update.View;
        TurnSecondsLeft = update.TurnSecondsLeft;
        foreach (var e in update.Events)
        {
            if (e is CardPlayed played && GameText.Banner(played) is { } banner)
            {
                BannerCount++;
                BannerText = banner;
            }

            if (GameText.Describe(e, Me, SeatName) is { } line)
            {
                _feed = _feed.Add(line);
                if (_feed.Count > FeedLength)
                {
                    _feed = _feed.RemoveAt(0);
                }
            }

            switch (e)
            {
                case RoundScored scored:
                    PendingSummary = scored;
                    break;
                case MatchEnded ended:
                    Result = ended;
                    break;
            }
        }

        Changed?.Invoke();
    }

    private void OnRevealed(MatchReveal reveal)
    {
        Verified = Room?.Commitment is { } commitment && _view is not null
            && Commitment.Verify(commitment, reveal, _view.Score);
        Changed?.Invoke();
    }
}

/// <summary>
/// The online game and its server (ADR 0014): the server in an invite link, else the one remembered on this device,
/// else the one built into the app (<c>ServerUrl</c> in appsettings, empty on GitHub Pages).
/// </summary>
public sealed class OnlineService(IConfiguration configuration, IOnlineSeatStore store, IJSRuntime js)
{
    private const string Key = "spincio.server";
    private RemoteGameSession? _session;
    private Uri? _server;
    private Task? _loading;

    public Uri? ServerUrl => _server ?? BuiltInServer;

    /// <summary>The server built into the app (ADR 0016: Render), if any.</summary>
    public Uri? BuiltInServer => Parse(configuration["ServerUrl"]);

    /// <summary>A server other than the built-in one, from an invite link or typed in: it can be dropped.</summary>
    public bool UsesOtherServer => _server is not null && _server != BuiltInServer;

    public RemoteGameSession Session =>
        _session ??= new RemoteGameSession(ServerUrl ?? throw new InvalidOperationException("No server chosen."), store);

    /// <summary>
    /// Reads the server remembered on this device, once: the invitation bar and the online page both ask at start-up,
    /// and both wait for the same reading.
    /// </summary>
    public Task LoadAsync() => _loading ??= ReadAsync();

    /// <summary>Switches to <paramref name="address"/> and remembers it. False if it is not a usable server address.</summary>
    public async Task<bool> UseServerAsync(string? address)
    {
        if (Parse(address) is not { } url)
        {
            return false;
        }

        if (url != ServerUrl)
        {
            await ResetAsync();
        }

        _server = url;
        await StoreAsync(url.ToString());
        return true;
    }

    /// <summary>Forgets the chosen server (e.g. a temporary one that has been switched off).</summary>
    public async Task ForgetServerAsync()
    {
        await ResetAsync();
        _server = null;
        await StoreAsync(null);
    }

    public async Task ResetAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    /// <summary>
    /// A server address the app can use: https (http only on this computer, for development), reduced to its origin.
    /// Null otherwise.
    /// </summary>
    public static Uri? Parse(string? address)
    {
        if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var url)
            || !(url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback)))
        {
            return null;
        }

        return new Uri(url.GetLeftPart(UriPartial.Authority) + "/");
    }

    /// <summary>The link that opens the online page already connected to <paramref name="server"/>, with the room code filled in.</summary>
    public static string InviteLink(string appBase, Uri server, string roomCode)
    {
        ArgumentNullException.ThrowIfNull(server);
        return $"{appBase}online?server={Uri.EscapeDataString(server.GetLeftPart(UriPartial.Authority))}&room={Uri.EscapeDataString(roomCode)}";
    }

    private async Task ReadAsync()
    {
        try
        {
            var remembered = Parse(await js.InvokeAsync<string?>("localStorage.getItem", Key));
            _server ??= remembered; // a server from an invite link, chosen meanwhile, wins
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }

    private async Task StoreAsync(string? value)
    {
        try
        {
            if (value is null)
            {
                await js.InvokeVoidAsync("localStorage.removeItem", Key);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.setItem", Key, value);
            }
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }
}
