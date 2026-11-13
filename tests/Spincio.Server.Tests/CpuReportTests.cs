using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spincio.Bots;
using Spincio.Contracts;
using Spincio.Engine;
using Spincio.Server.Feedback;

namespace Spincio.Server.Tests;

/// <summary>ADR 0020: players' reports about CPU moves, checked by replaying the match, then filed as issues.</summary>
public class CpuReportTests
{
    /// <summary>A real match played by L1, cut right after the partner's (seat 2) <paramref name="nth"/> play.</summary>
    internal static CpuMoveReport Report(ulong seed = 5, int nth = 3, GameVariant variant = GameVariant.Spincio, string text = "Doveva prendere il settebello")
    {
        var bot = new GreedyBot();
        var memories = Seat.All.Select(s => new BotMemory(s, variant)).ToArray();
        var rng = new Pcg32(seed, 1);
        var transition = SpincioEngine.NewMatch(seed, variant: variant);
        var commands = new List<string>();
        int partnerPlays = 0;
        while (true)
        {
            foreach (var memory in memories)
            {
                memory.Observe(transition.EventsFor(memory.Seat));
            }

            var seat = transition.State.ToPlay;
            var command = bot.Choose(SpincioEngine.ViewFor(transition.State, seat), memories[seat.Index], ref rng);
            commands.Add(CommandCodec.Encode(command));
            if (command is PlayCard && seat.Index == 2 && ++partnerPlays == nth)
            {
                return new CpuMoveReport("1.14.0", "1.7", variant, "Normale", seed, commands, ["gave-sweep"], null, text);
            }

            transition = SpincioEngine.Apply(transition.State, command).Value;
        }
    }

    private static MatchState Before(CpuMoveReport report)
    {
        var state = SpincioEngine.NewMatch(report.Seed, variant: report.Variant).State;
        foreach (var command in report.Commands.Take(report.Commands.Count - 1))
        {
            state = SpincioEngine.Apply(state, CommandCodec.Decode(command)).Value.State;
        }

        return state;
    }

    [Fact]
    public void A_report_of_a_real_partner_move_is_accepted()
    {
        CpuMoveReports.Problem(Report()).ShouldBeNull();
        CpuMoveReports.Problem(Report(seed: 8, nth: 20, variant: GameVariant.Spincione)).ShouldBeNull();
    }

    [Fact]
    public void A_report_that_does_not_replay_or_says_nothing_is_refused()
    {
        var report = Report();
        var commands = report.Commands.ToList();
        commands[3] = commands[3] == "P0:KD" ? "P0:KC" : "P0:KD";
        string mine = commands.First(c => c.StartsWith("P0", StringComparison.Ordinal));

        CpuMoveReports.Problem(report with { Commands = commands }).ShouldNotBeNull(); // a changed move
        CpuMoveReports.Problem(report with { Commands = [.. report.Commands.TakeWhile(c => c != mine), mine] }).ShouldNotBeNull(); // my own move
        CpuMoveReports.Problem(report with { Text = new string('x', CpuMoveReports.MaxTextLength + 1) }).ShouldNotBeNull();
        CpuMoveReports.Problem(report with { Text = " ", Reasons = [] }).ShouldNotBeNull(); // says nothing
        CpuMoveReports.Problem(report with { Reasons = ["sweep", "hack"] }).ShouldNotBeNull();
        CpuMoveReports.Problem(report with { Variant = (GameVariant)7 }).ShouldNotBeNull();
        CpuMoveReports.Problem(report with { Commands = [] }).ShouldNotBeNull();
        CpuMoveReports.Problem(null).ShouldNotBeNull();
    }

    [Fact]
    public void The_better_card_must_be_one_the_cpu_held()
    {
        var report = Report();
        var hand = Before(report).HandOf(new Seat(2));
        var notHeld = Card.FullDeck.First(c => !hand.Contains(c));

        CpuMoveReports.Problem(report with { BetterCard = hand[0].ToString() }).ShouldBeNull();
        CpuMoveReports.Problem(report with { BetterCard = notHeld.ToString() }).ShouldNotBeNull();
        CpuMoveReports.Problem(report with { BetterCard = "ZZ" }).ShouldNotBeNull();
    }

    [Fact]
    public void The_issue_shows_the_position_the_words_and_the_report_to_replay()
    {
        var report = Report(text: "Ciao @mattia, guarda #12:\nnon doveva calare");
        var before = Before(report);

        var (title, body) = CpuReportIssue.Format(report);

        title.ShouldStartWith("CPU: " + report.Commands[^1]);
        body.ShouldContain("> Ciao @​mattia, guarda #​12:\n> non doveva calare"); // no pings, no issue links
        body.ShouldContain("Ha lasciato lo spazzino");
        body.ShouldContain($"seme {report.Seed}");
        body.ShouldContain("**Tavola prima della mossa:** " + (before.Table.IsEmpty ? "(vuota)" : string.Join(" ", before.Table)));
        body.ShouldContain("**Mano della CPU:** " + string.Join(" ", before.HandOf(new Seat(2))));
        string json = body[(body.IndexOf("```json\n", StringComparison.Ordinal) + 8)..body.LastIndexOf("\n```", StringComparison.Ordinal)];
        var back = JsonSerializer.Deserialize<CpuMoveReport>(json, SpincioJson.Options)!;
        back.Commands.ShouldBe(report.Commands);
        back.Text.ShouldBe(report.Text);
    }

    [Fact]
    public async Task The_github_sink_opens_an_issue_with_the_token()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var sink = new GitHubIssueSink(
            new HttpClient(handler),
            Options.Create(new FeedbackOptions { GitHubRepository = "owner/feedback", GitHubToken = "secret" }),
            NullLogger<GitHubIssueSink>.Instance);

        (await sink.SendAsync(Report(), CancellationToken.None)).ShouldBeTrue();

        handler.Request!.RequestUri.ShouldBe(new Uri("https://api.github.com/repos/owner/feedback/issues"));
        handler.Request.Headers.Authorization!.ToString().ShouldBe("Bearer secret");
        using var sent = JsonDocument.Parse(handler.Body!);
        sent.RootElement.GetProperty("labels")[0].GetString().ShouldBe(CpuReportIssue.Label);
        sent.RootElement.GetProperty("title").GetString().ShouldStartWith("CPU: ");

        var refused = new GitHubIssueSink(
            new HttpClient(new RecordingHandler(HttpStatusCode.Unauthorized)),
            Options.Create(new FeedbackOptions { GitHubRepository = "owner/feedback", GitHubToken = "wrong" }),
            NullLogger<GitHubIssueSink>.Instance);
        (await refused.SendAsync(Report(), CancellationToken.None)).ShouldBeFalse();
        new GitHubIssueSink(new HttpClient(handler), Options.Create(new FeedbackOptions()), NullLogger<GitHubIssueSink>.Instance)
            .IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task The_endpoint_files_genuine_reports_and_refuses_the_rest()
    {
        var sink = new FakeSink();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Feedback:PerClientPerHour", "3")
            .ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ICpuReportSink>(sink))));
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync(CpuReportEndpoints.Path, Report(), SpincioJson.Options)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        sink.Received.ShouldHaveSingleItem().Seed.ShouldBe(5UL);
        (await client.PostAsJsonAsync(CpuReportEndpoints.Path, Report() with { Reasons = ["hack"] }, SpincioJson.Options)).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
        sink.Configured = false;
        (await client.PostAsJsonAsync(CpuReportEndpoints.Path, Report(), SpincioJson.Options)).StatusCode
            .ShouldBe(HttpStatusCode.ServiceUnavailable); // the phone keeps it and retries
        (await client.PostAsJsonAsync(CpuReportEndpoints.Path, Report(), SpincioJson.Options)).StatusCode
            .ShouldBe(HttpStatusCode.TooManyRequests); // 3 per hour from one address
        sink.Received.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Without_a_destination_the_server_says_to_retry_later()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync(CpuReportEndpoints.Path, Report(), SpincioJson.Options)).StatusCode
            .ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private sealed class FakeSink : ICpuReportSink
    {
        public List<CpuMoveReport> Received { get; } = [];

        public bool Configured { get; set; } = true;

        public bool IsConfigured => Configured;

        public Task<bool> SendAsync(CpuMoveReport report, CancellationToken cancellationToken)
        {
            Received.Add(report);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
}
