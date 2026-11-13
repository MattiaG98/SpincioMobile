using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Spincio.Contracts;

namespace Spincio.Server.Feedback;

/// <summary>Where CPU move reports go (ADR 0020). Bound from the "Feedback" configuration section.</summary>
public sealed class FeedbackOptions
{
    /// <summary>"owner/name" of the GitHub repository that collects the reports as issues (best a private one).</summary>
    public string? GitHubRepository { get; set; }

    /// <summary>
    /// Token allowed to open issues there and nothing else (a fine-grained token: that repository, Issues read and write).
    /// A secret: on Render, <c>Feedback__GitHubToken</c>, never in a file.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>Reports accepted from one client per hour.</summary>
    public int PerClientPerHour { get; set; } = 10;
}

/// <summary>Stores accepted reports somewhere people can read them.</summary>
public interface ICpuReportSink
{
    /// <summary>False until a destination is configured: reports then wait on the phones.</summary>
    bool IsConfigured { get; }

    /// <summary>True once stored.</summary>
    Task<bool> SendAsync(CpuMoveReport report, CancellationToken cancellationToken);
}

/// <summary>Opens one GitHub issue per report (<see cref="CpuReportIssue"/>).</summary>
public sealed class GitHubIssueSink(HttpClient http, IOptions<FeedbackOptions> options, ILogger<GitHubIssueSink> logger) : ICpuReportSink
{
    private static readonly Action<ILogger, string, int, Exception?> Rejected = LoggerMessage.Define<string, int>(
        LogLevel.Warning, new EventId(1, "CpuReportRejected"), "GitHub refused a CPU report for {Repository}: HTTP {Status}");

    private static readonly Action<ILogger, string, Exception?> Failed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2, "CpuReportFailed"), "Could not reach GitHub for {Repository}");

    private readonly FeedbackOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.GitHubRepository) && !string.IsNullOrWhiteSpace(_options.GitHubToken);

    public async Task<bool> SendAsync(CpuMoveReport report, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var (title, body) = CpuReportIssue.Format(report);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://api.github.com/repos/{_options.GitHubRepository}/issues"))
        {
            Content = JsonContent.Create(new { title, body, labels = new[] { CpuReportIssue.Label } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("spincio-server");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Rejected(logger, _options.GitHubRepository!, (int)response.StatusCode, null);
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException e)
        {
            Failed(logger, _options.GitHubRepository!, e);
            return false;
        }
    }
}
