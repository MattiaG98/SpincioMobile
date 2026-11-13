using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Spincio.Contracts;

namespace Spincio.Client.Game;

public enum ReportDelivery
{
    /// <summary>The server stored it.</summary>
    Delivered,

    /// <summary>The server said it is not a valid report: sending it again would not help.</summary>
    Rejected,

    /// <summary>Offline, server asleep or busy, or no destination yet: try again later.</summary>
    RetryLater,
}

/// <summary>Sends one report to the server.</summary>
public interface ICpuReportTransport
{
    Task<ReportDelivery> SendAsync(CpuMoveReport report);
}

/// <summary><c>POST /api/cpu-reports</c> on the app's own server (<c>ServerUrl</c> in appsettings, ADR 0016).</summary>
public sealed class HttpCpuReportTransport(IConfiguration configuration) : ICpuReportTransport
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) }; // a sleeping server takes up to a minute

    public async Task<ReportDelivery> SendAsync(CpuMoveReport report)
    {
        if (!Uri.TryCreate(configuration["ServerUrl"], UriKind.Absolute, out var server))
        {
            return ReportDelivery.RetryLater;
        }

        try
        {
            using var response = await Http.PostAsJsonAsync(new Uri(server, "api/cpu-reports"), report, SpincioJson.Options);
            return response.IsSuccessStatusCode ? ReportDelivery.Delivered
                : response.StatusCode == HttpStatusCode.BadRequest ? ReportDelivery.Rejected
                : ReportDelivery.RetryLater;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return ReportDelivery.RetryLater;
        }
    }
}

/// <summary>
/// The player's reports about CPU moves (ADR 0020). Kept on the device until the server takes them, so nothing is lost
/// offline or while the server sleeps; sent one after the other, oldest first.
/// </summary>
public sealed class CpuReportService(IJSRuntime js, ICpuReportTransport transport)
{
    private const string Key = "spincio.cpuReports";
    private List<CpuMoveReport>? _pending;
    private Task? _flushing;

    /// <summary>Raised when the number of reports waiting changes.</summary>
    public event Action? Changed;

    /// <summary>Reports waiting to be sent.</summary>
    public async Task<int> PendingAsync() => (await LoadAsync()).Count;

    /// <summary>Keeps the report and starts sending; returns at once (the server may be asleep for a minute).</summary>
    public async Task SubmitAsync(CpuMoveReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var pending = await LoadAsync();
        pending.Add(report);
        await SaveAsync(pending);
        Changed?.Invoke();
        _ = FlushAsync();
    }

    /// <summary>Sends what is waiting, oldest first; stops at the first that has to wait. One flush at a time.</summary>
    public Task FlushAsync() => _flushing is { IsCompleted: false } running ? running : _flushing = SendAllAsync();

    private async Task SendAllAsync()
    {
        var pending = await LoadAsync();
        while (pending.Count > 0)
        {
            if (await transport.SendAsync(pending[0]) == ReportDelivery.RetryLater)
            {
                return;
            }

            pending.RemoveAt(0); // delivered, or never deliverable
            await SaveAsync(pending);
            Changed?.Invoke();
        }
    }

    private async Task<List<CpuMoveReport>> LoadAsync()
    {
        if (_pending is not null)
        {
            return _pending;
        }

        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            _pending = string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<CpuMoveReport>>(json, SpincioJson.Options) ?? [];
        }
        catch (Exception e) when (e is JSException or JsonException or InvalidOperationException)
        {
            _pending = [];
        }

        return _pending;
    }

    private async Task SaveAsync(List<CpuMoveReport> pending)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(pending, SpincioJson.Options));
        }
        catch (Exception e) when (e is JSException or InvalidOperationException)
        {
        }
    }
}
