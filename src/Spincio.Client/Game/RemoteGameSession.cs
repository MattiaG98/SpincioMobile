using System.Collections.Immutable;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Client.Game;

/// <summary>Remembers the secret seat token, so a player can come back after closing the page.</summary>
public interface IOnlineSeatStore
{
    Task<string?> LoadAsync();

    Task SaveAsync(string token);

    Task ClearAsync();
}

public sealed class LocalStorageOnlineSeatStore(IJSRuntime js) : IOnlineSeatStore
{
    private const string Key = "spincio.onlineSeat";

    public async Task<string?> LoadAsync() => await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async Task SaveAsync(string token) => await js.InvokeVoidAsync("localStorage.setItem", Key, token);

    public async Task ClearAsync() => await js.InvokeVoidAsync("localStorage.removeItem", Key);
}

public sealed class InMemoryOnlineSeatStore : IOnlineSeatStore
{
    public string? Token { get; private set; }

    public Task<string?> LoadAsync() => Task.FromResult(Token);

    public Task SaveAsync(string token)
    {
        Token = token;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Token = null;
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
    }

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

    public int SweepCount { get; private set; }

    public IMoveAnimator? Animator { get; set; }

    /// <summary>Seconds left for this player's move, as of the last update (0 when it is not our turn).</summary>
    public int TurnSecondsLeft { get; private set; }

    /// <summary>Null until the match ends; then whether the revealed seed matched the commitment and the replay.</summary>
    public bool? Verified { get; private set; }

    public string Footer
    {
        get
        {
            var room = Room is null ? "Online" : $"Stanza {Room.Code}";
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

    public async Task<JoinResult> CreateRoomAsync(string playerName)
    {
        await ConnectAsync();
        return await AfterJoinAsync(await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), playerName));
    }

    public async Task<JoinResult> JoinRoomAsync(string roomCode, string playerName)
    {
        await ConnectAsync();
        return await AfterJoinAsync(await _hub.InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), roomCode, playerName));
    }

    /// <summary>Comes back to the match remembered in the seat store, if any.</summary>
    public async Task<bool> TryRejoinAsync()
    {
        _token = await _store.LoadAsync();
        if (string.IsNullOrEmpty(_token))
        {
            return false;
        }

        await ConnectAsync();
        var result = await RejoinAsync();
        if (result is not { Ok: true })
        {
            await _store.ClearAsync();
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

    public async Task LeaveAsync()
    {
        await _store.ClearAsync();
        _token = null;
        await _hub.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _hub.DisposeAsync();
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
        if (result.Ok)
        {
            Me = new Seat(result.Seat);
        }

        return result;
    }

    private async Task<JoinResult> AfterJoinAsync(JoinResult result)
    {
        if (result is { Ok: true, Token: { } token })
        {
            _token = token;
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
            if (e is CardPlayed { IsSweep: true })
            {
                SweepCount++;
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

/// <summary>Creates the online session when a server is configured (<c>ServerUrl</c> in appsettings.json).</summary>
public sealed class OnlineService(IConfiguration configuration, IOnlineSeatStore store)
{
    private RemoteGameSession? _session;

    public Uri? ServerUrl =>
        Uri.TryCreate(configuration["ServerUrl"], UriKind.Absolute, out var url) ? url : null;

    public RemoteGameSession Session =>
        _session ??= new RemoteGameSession(ServerUrl ?? throw new InvalidOperationException("No ServerUrl configured."), store);

    public async Task ResetAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }
}
