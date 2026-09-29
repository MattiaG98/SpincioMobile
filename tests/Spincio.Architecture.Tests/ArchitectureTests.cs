using System.Reflection;
using NetArchTest.Rules;
using Spincio.Engine;

namespace Spincio.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Engine = typeof(Card).Assembly;
    private static readonly Assembly Bots = typeof(Spincio.Bots.IBot).Assembly;
    private static readonly Assembly Client = typeof(Spincio.Client.Game.LocalGameSession).Assembly;

    [Fact]
    public void Engine_references_only_the_base_class_library()
    {
        var foreign = Engine.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n != "netstandard" && n != "mscorlib")
            .ToList();

        foreign.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("System.Random")]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.TimeProvider")]
    [InlineData("System.Guid")]
    [InlineData("System.Environment")]
    [InlineData("System.IO")]
    [InlineData("System.Net")]
    [InlineData("System.Threading")]
    [InlineData("System.Security.Cryptography")]
    [InlineData("System.Diagnostics.Stopwatch")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Spincio.Bots")]
    [InlineData("Spincio.Client")]
    public void Engine_does_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(Engine).ShouldNot().HaveDependencyOn(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Spincio.Client")]
    [InlineData("Microsoft.AspNetCore")]
    public void Bots_do_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(Bots).ShouldNot().HaveDependencyOn(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// CLAUDE.md rule 5 / ADR 0008: bots never receive the real state. Only the L2 search bot handles
    /// <c>MatchState</c>, and only hypothetical ones it builds from its own view via <c>SpincioEngine.Hypothetical</c>.
    /// </summary>
    [Fact]
    public void Only_the_search_bot_handles_match_states()
    {
        var result = Types.InAssembly(Bots).ShouldNot().HaveDependencyOn("Spincio.Engine.MatchState").GetResult();

        (result.FailingTypeNames ?? [])
            .Where(n => !n.StartsWith("Spincio.Bots.PimcBot", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Bots_are_given_only_a_player_view()
    {
        var parameters = typeof(Spincio.Bots.IBot).GetMethod(nameof(Spincio.Bots.IBot.Choose))!
            .GetParameters()
            .Select(p => p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType);

        parameters.ShouldBe([typeof(PlayerView), typeof(Spincio.Bots.BotMemory), typeof(Pcg32)]);
    }

    /// <summary>CLAUDE.md rule 6: Client and Server share Engine, Bots and Contracts, never each other.</summary>
    [Fact]
    public void Client_and_server_never_reference_each_other()
    {
        var server = typeof(Spincio.Server.Hubs.GameHub).Assembly;

        server.GetReferencedAssemblies().Select(a => a.Name).ShouldNotContain("Spincio.Client");
        Client.GetReferencedAssemblies().Select(a => a.Name).ShouldNotContain("Spincio.Server");
        typeof(Spincio.Contracts.GameUpdate).Assembly.GetReferencedAssemblies().Select(a => a.Name)
            .ShouldNotContain(n => n == "Spincio.Client" || n == "Spincio.Server" || n == "Spincio.Bots");
    }

    /// <summary>CLAUDE.md rule 4: the UI renders only the human's PlayerView; the full state stays in the session.</summary>
    [Theory]
    [InlineData("Spincio.Client.Pages")]
    [InlineData("Spincio.Client.Components")]
    [InlineData("Spincio.Client.Layout")]
    public void Ui_never_touches_the_full_match_state(string uiNamespace)
    {
        var result = Types.InAssembly(Client).That().ResideInNamespace(uiNamespace)
            .ShouldNot().HaveDependencyOn("Spincio.Engine.MatchState").GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
