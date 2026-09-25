namespace Spincio.Bots.Tests;

public class BotsSmokeTests
{
    [Fact]
    public void Bots_assembly_loads() => typeof(BotsAssembly).Assembly.ShouldNotBeNull();
}
