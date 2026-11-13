using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spincio.Bots;
using Spincio.Client.Components;
using Spincio.Client.Game;
using Spincio.Contracts;
using Spincio.Engine;

namespace Spincio.Client.Tests;

/// <summary>ADR 0020 on the phone: the partner's moves, the report panel, and reports that wait for the server.</summary>
public class CpuReportClientTests : BunitContext
{
    private readonly FakeTransport _transport = new();

    public CpuReportClientTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICpuReportTransport>(_transport);
        Services.AddSingleton<CpuReportService>();
    }

    /// <summary>An offline match where I (seat 0) play like L1, stopped after <paramref name="myPlays"/> of my plays.</summary>
    private static async Task<LocalGameSession> MatchAsync(ulong seed, int myPlays)
    {
        var session = new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask);
        await session.NewGameAsync(seed);
        var bot = new GreedyBot();
        var memory = new BotMemory(LocalGameSession.Human);
        var rng = Pcg32.FromSeed(3);
        for (int i = 0; i < myPlays && session.Result is null; i++)
        {
            if (session.PendingSummary is not null)
            {
                await session.AcknowledgeSummaryAsync();
            }

            await session.PlayAsync(bot.Choose(session.View, memory, ref rng));
        }

        return session;
    }

    [Fact]
    public async Task The_partner_s_moves_come_newest_first_with_their_position()
    {
        var session = await MatchAsync(seed: 11, myPlays: 7); // two deals done, the third under way

        var moves = MoveReview.MovesOf(LocalGameSession.Partner, session.Seed, session.Variant, session.Commands);

        moves.Count.ShouldBeGreaterThanOrEqualTo(6);
        moves.Select(m => m.CommandCount).ShouldBe(moves.Select(m => m.CommandCount).OrderDescending().ToList());
        foreach (var move in moves)
        {
            var command = (PlayCard)CommandCodec.Decode(session.Commands[move.CommandCount - 1]);
            command.Seat.ShouldBe(LocalGameSession.Partner);
            command.Card.ShouldBe(move.Play.Card);
        }

        // The partner's hand of the current deal stays secret; finished deals show it.
        var current = session.View.DealNumber;
        moves.Where(m => m.DealNumber == current && m.RoundNumber == session.View.RoundNumber).ShouldAllBe(m => m.HandBefore == null);
        var done = moves[^1]; // the first move of the match
        done.HandBefore.ShouldNotBeNull().ShouldContain(done.Play.Card);
        done.HandBefore!.Value.Length.ShouldBe(3);
    }

    [Fact]
    public async Task Reports_wait_on_the_phone_until_the_server_takes_them()
    {
        var service = Services.GetRequiredService<CpuReportService>();
        var session = await MatchAsync(seed: 11, myPlays: 4);
        var move = MoveReview.MovesOf(LocalGameSession.Partner, session.Seed, session.Variant, session.Commands)[0];
        var report = new CpuMoveReport("1", "1.7", session.Variant, "Normale", session.Seed, [.. session.Commands.Take(move.CommandCount)], ["other"], null, "no");

        _transport.Next = ReportDelivery.RetryLater; // offline, or the server is asleep
        await service.SubmitAsync(report); // kept, and tried at once
        (await service.PendingAsync()).ShouldBe(1);

        _transport.Next = ReportDelivery.Delivered;
        await service.FlushAsync(); // e.g. the next time the app opens
        (await service.PendingAsync()).ShouldBe(0);
        _transport.Sent.Count.ShouldBe(2); // tried, then delivered

        _transport.Next = ReportDelivery.Rejected; // never deliverable: not kept forever
        await service.SubmitAsync(report);
        (await service.PendingAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task The_panel_reports_the_chosen_move_with_the_match_up_to_it()
    {
        var session = await MatchAsync(seed: 11, myPlays: 7);
        var moves = MoveReview.MovesOf(LocalGameSession.Partner, session.Seed, session.Variant, session.Commands);
        _transport.Next = ReportDelivery.Delivered;

        var panel = Render<ReportPanel>(p => p.Add(x => x.Session, session));

        panel.Find("#report-title").TextContent.ShouldBe("Segnala una mossa di Titti");
        panel.FindAll(".report-move").Count.ShouldBe(Math.Min(6, moves.Count));
        panel.Find(".report-actions .btn").HasAttribute("disabled").ShouldBeTrue(); // nothing chosen yet
        panel.Find(".report-actions .btn").TextContent.Trim().ShouldBe("Scegli una mossa");

        var older = moves[^1]; // the first move: its deal is over, so the hand shows and a better card can be picked
        panel.Find(".report-more").Click();
        panel.FindAll(".report-move")[^1].Click();
        panel.FindAll(".report-move").Count.ShouldBe(1); // the list folds to the chosen move
        panel.Find(".report-actions .btn").HasAttribute("disabled").ShouldBeTrue(); // nothing said yet
        panel.Find(".report-actions .btn").TextContent.Trim().ShouldBe("Dicci cosa non va");
        panel.FindAll(".chip").Single(c => c.TextContent == "Ha lasciato lo spazzino").Click();
        var better = older.HandBefore!.Value.First(c => c != older.Play.Card);
        panel.Find($".pick [data-card='{better}']").Click();
        panel.Find("textarea").Input("Doveva tenersi il sette");
        panel.Find(".report-actions .btn").TextContent.Trim().ShouldBe("Invia segnalazione");
        panel.Find(".report-actions .btn").Click();

        panel.WaitForAssertion(() => _transport.Sent.ShouldHaveSingleItem());
        var sent = _transport.Sent[0];
        sent.Commands.ShouldBe(session.Commands.Take(older.CommandCount));
        sent.Reasons.ShouldBe(["gave-sweep"]);
        sent.BetterCard.ShouldBe(better.ToString());
        sent.Text.ShouldBe("Doveva tenersi il sette");
        sent.Level.ShouldBe("Normale");
        CpuMoveReports.Problem(sent).ShouldBeNull();
        panel.Find("#report-title").TextContent.ShouldBe("Grazie!");
    }

    [Fact]
    public async Task Before_the_partner_plays_there_is_nothing_to_report()
    {
        LocalGameSession session;
        ulong seed = 0;
        do
        {
            session = new LocalGameSession(new InMemoryGameStore(), delay: _ => Task.CompletedTask);
            await session.NewGameAsync(++seed); // a match where Titti has not played yet when my turn comes
        }
        while (session.Commands.Any(c => c.StartsWith("P2", StringComparison.Ordinal)));

        var panel = Render<ReportPanel>(p => p.Add(x => x.Session, session));

        panel.Markup.ShouldContain("non ha ancora giocato");
        panel.FindAll(".report-move").ShouldBeEmpty();
    }

    private sealed class FakeTransport : ICpuReportTransport
    {
        public ReportDelivery Next { get; set; } = ReportDelivery.Delivered;

        public List<CpuMoveReport> Sent { get; } = [];

        public Task<ReportDelivery> SendAsync(CpuMoveReport report)
        {
            Sent.Add(report);
            return Task.FromResult(Next);
        }
    }
}
