using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Spincio.Contracts;

namespace Spincio.Server.Feedback;

/// <summary><c>POST /api/cpu-reports</c>: players' reports about CPU moves (ADR 0020).</summary>
public static class CpuReportEndpoints
{
    public const string Path = "/api/cpu-reports";
    private const string RatePolicy = "cpu-reports";

    public static IServiceCollection AddCpuReports(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeedbackOptions>(configuration.GetSection("Feedback"));
        services.AddHttpClient<ICpuReportSink, GitHubIssueSink>();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(RatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context.RequestServices.GetRequiredService<IOptions<FeedbackOptions>>().Value.PerClientPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
        });
        return services;
    }

    public static void MapCpuReports(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost(Path, AcceptAsync)
            .RequireRateLimiting(RatePolicy)
            .WithMetadata(new RequestSizeLimitAttribute(256 * 1024));
    }

    /// <summary>
    /// 202 stored · 400 not a genuine report (it must replay) · 429 too many · 503 no destination configured yet ·
    /// 502 the destination failed. The app keeps a report and retries on 429 and 5xx.
    /// </summary>
    private static async Task<IResult> AcceptAsync(CpuMoveReport report, ICpuReportSink sink, CancellationToken cancellationToken)
    {
        if (CpuMoveReports.Problem(report) is { } problem)
        {
            return Results.BadRequest(new { error = problem });
        }

        if (!sink.IsConfigured)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return await sink.SendAsync(report, cancellationToken) ? Results.Accepted() : Results.StatusCode(StatusCodes.Status502BadGateway);
    }

    /// <summary>The player's address: Render's proxy puts it first in X-Forwarded-For.</summary>
    private static string ClientKey(HttpContext context)
    {
        string forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        return forwarded.Length > 0 ? forwarded.Split(',')[0].Trim() : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
