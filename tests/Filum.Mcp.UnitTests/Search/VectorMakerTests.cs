using Filum.Mcp.UnitTests.Builders;
using Filum.Mcp.UnitTests.Fakes;

namespace Filum.Mcp.UnitTests.Search;

public sealed class VectorMakerTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task MakeSoon_WhenTheModelIsNotThere_ShouldMakeNothing()
    {
        await _server.LogToolStrategy().LogAsync("The spare key is under the blue pot.", null, CancellationToken.None);

        var making = _server.Vectors.MakeSoon();

        Assert.Same(Task.CompletedTask, making);
    }

    [Fact]
    public async Task MakeSoon_WhenTheModelIsThere_ShouldMakeTheMissingVectors()
    {
        await _server.LogToolStrategy().LogAsync("The spare key is under the blue pot.", null, CancellationToken.None);
        _server.Model.TryFill(new WordEmbedder());

        await _server.Vectors.MakeSoon();

        Assert.Equal(0, await _server.Engines.Current.Memory.MakeVectorsAsync(_server.Memory.Owner, int.MaxValue, CancellationToken.None));
    }
}
