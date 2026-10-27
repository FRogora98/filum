using Filum.Mcp.UnitTests.Builders;
using Filum.Mcp.UnitTests.Fakes;

namespace Filum.Mcp.UnitTests.Session;

public sealed class McpEnginesTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void Current_WhenTheModelIsNotThere_ShouldBeOneEngineByWords()
    {
        Assert.Same(_server.Engines.Current, _server.Engines.Current);
    }

    [Fact]
    public void Current_WhenTheModelArrives_ShouldBeAnotherEngineMadeOnce()
    {
        var byWords = _server.Engines.Current;

        _server.Model.TryFill(new WordEmbedder());

        Assert.NotSame(byWords, _server.Engines.Current);
        Assert.Same(_server.Engines.Current, _server.Engines.Current);
    }

    [Fact]
    public async Task Current_WhenAskedFromManyThreadsAtOnce_ShouldMakeOneEngine()
    {
        var engines = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => _server.Engines.Current)));

        Assert.Single(engines.Distinct());
    }

    [Fact]
    public async Task Current_WhenEitherEngineWritesAnEvent_ShouldReadTheSameMemory()
    {
        await _server.LogToolStrategy().LogAsync("The spare key is under the blue pot.", null, CancellationToken.None);
        _server.Model.TryFill(new WordEmbedder());

        var found = await _server.Engines.Current.Memory.PendingAsync(_server.Memory.Owner, 10, CancellationToken.None);

        Assert.Single(found);
    }
}
