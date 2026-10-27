using Filum.Engine.Abstractions.Events;
using Filum.Mcp.UnitTests.Builders;
using ModelContextProtocol;

namespace Filum.Mcp.UnitTests.Tools;

public sealed class LogToolStrategyTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task LogAsync_WhenThePersonSaysSomething_ShouldKeepItTrimmedWithItsDateInThisSession()
    {
        var answer = await _server.LogToolStrategy().LogAsync("  I moved to the coast in March. ", "2026-03-02", CancellationToken.None);

        Assert.StartsWith("Logged (event 1): their words are kept, but nothing is saved yet.", answer);
        Assert.Contains("do not say it is saved", answer);
        var said = Assert.Single(await _server.Engines.Current.Memory.EventsAsync(_server.Memory.Owner, new EventQuery(), CancellationToken.None));
        Assert.Equal((MemoryEventKind.Said, MemoryEventSource.Host, "I moved to the coast in March.", _server.Session.Id), (said.Kind, said.Source, said.Text, said.ConversationId));
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), said.OccurredAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogAsync_WhenTheTextIsBlank_ShouldRefuse(string text)
    {
        var refused = await Assert.ThrowsAsync<McpException>(() => _server.LogToolStrategy().LogAsync(text, null, CancellationToken.None));

        Assert.Equal("Refused: the text must be between 1 and 8000 characters.", refused.Message);
    }

    [Fact]
    public async Task LogAsync_WhenTheTextIsLongerThanTheLimit_ShouldRefuseAndKeepNothing()
    {
        await Assert.ThrowsAsync<McpException>(() => _server.LogToolStrategy().LogAsync(new string('a', McpConstants.Limits.MaxText + 1), null, CancellationToken.None));

        Assert.Empty(await _server.Engines.Current.Memory.EventsAsync(_server.Memory.Owner, new EventQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task LogAsync_WhenTheDateIsNotADate_ShouldRefuse()
    {
        var refused = await Assert.ThrowsAsync<McpException>(() => _server.LogToolStrategy().LogAsync("x", "someday", CancellationToken.None));

        Assert.StartsWith("Refused: occurredAt", refused.Message);
    }

    [Fact]
    public void Function_WhenMade_ShouldBeNamedMemoryLog()
    {
        Assert.Equal("memory_log", _server.LogToolStrategy().Function().Name);
    }
}
