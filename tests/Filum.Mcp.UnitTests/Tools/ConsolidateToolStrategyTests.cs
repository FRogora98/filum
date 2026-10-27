using Filum.Mcp.Tools;
using Filum.Mcp.UnitTests.Builders;

namespace Filum.Mcp.UnitTests.Tools;

public sealed class ConsolidateToolStrategyTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task ConsolidateAsync_WhenNothingWasSaid_ShouldSayNothingIsWaiting()
    {
        Assert.Equal("Nothing is waiting to be tidied.", await _server.ConsolidateToolStrategy().ConsolidateAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task ConsolidateAsync_WhenSomethingWasSaid_ShouldHandItWithTheRulesAndTheIdToClose()
    {
        await _server.LogToolStrategy().LogAsync("I moved to the coast in March.", "2026-03-02", CancellationToken.None);

        var waiting = await _server.ConsolidateToolStrategy().ConsolidateAsync(null, CancellationToken.None);

        Assert.StartsWith(ConsolidationRules.Text, waiting);
        Assert.Contains("[event 1 · 2026-03-02", waiting);
        Assert.EndsWith("call memory_consolidate with through=1.", waiting);
    }

    [Fact]
    public void Text_ShouldAskForWhoThePersonIsTheirRulesAndOpenItemsAndALastCheck()
    {
        Assert.Contains("goes in the core", ConsolidationRules.Text);
        Assert.Contains(Filum.Engine.Instructions.InstructionParts.RulesAndOpenItems, ConsolidationRules.Text);
        Assert.Contains("An open item the events show done is ended now", ConsolidationRules.Text);
        Assert.Contains("Before closing, read the events once more", ConsolidationRules.Text);
    }

    [Fact]
    public async Task ConsolidateAsync_WhenMoreWaitsThanAPageHolds_ShouldHandOnePageAndSayTheRestWaits()
    {
        for (var i = 1; i <= 5; i++)
        {
            await _server.LogToolStrategy().LogAsync($"Note {i}: {new string('x', 6_000)}", null, CancellationToken.None);
        }

        var waiting = await _server.ConsolidateToolStrategy().ConsolidateAsync(null, CancellationToken.None);

        Assert.Contains("[event 2 ·", waiting);
        Assert.DoesNotContain("[event 3 ·", waiting);
        Assert.Contains("3 more events wait for a later session", waiting);
        Assert.EndsWith("call memory_consolidate with through=2.", waiting);
    }

    [Fact]
    public async Task ConsolidateAsync_WhenOneEventIsLongerThanAPage_ShouldStillHandIt()
    {
        await _server.LogToolStrategy().LogAsync(new string('x', 7_900), null, CancellationToken.None);
        await _server.LogToolStrategy().LogAsync(new string('y', 7_900), null, CancellationToken.None);
        await _server.LogToolStrategy().LogAsync(new string('z', 7_900), null, CancellationToken.None);

        var waiting = await _server.ConsolidateToolStrategy().ConsolidateAsync(null, CancellationToken.None);

        Assert.Contains("[event 1 ·", waiting);
        Assert.EndsWith("through=2.", waiting);
    }

    [Fact]
    public async Task ConsolidateAsync_WhenCalledWithThrough_ShouldCloseThePass()
    {
        await _server.LogToolStrategy().LogAsync("I moved to the coast in March.", null, CancellationToken.None);
        var tool = _server.ConsolidateToolStrategy();

        Assert.Equal("Done: 1 events tidied.", await tool.ConsolidateAsync(1, CancellationToken.None));
        Assert.Equal("Nothing is waiting to be tidied.", await tool.ConsolidateAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task ConsolidateAsync_WhenNothingUpToThatIdWaits_ShouldSaySo()
    {
        Assert.Equal("Nothing up to that id was waiting.", await _server.ConsolidateToolStrategy().ConsolidateAsync(7, CancellationToken.None));
    }
}
