using Filum.Engine.Abstractions.Events;
using Filum.Engine.IntegrationTests.Fakes;
using Filum.Engine.Storage.LocalFolder;
using Filum.Engine.Testing.Builders;
using Filum.Standalone;

namespace Filum.Engine.IntegrationTests;

public sealed class StandaloneAgentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"filum-standalone-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public async Task AskAsync_WhenTheModelIsOnesOwn_ShouldWriteTheMemoryThroughTheToolsAndLogTheMessage()
    {
        var model = new ScriptedModel();
        var agent = new StandaloneAgent(model, _folder);

        var answer = await agent.AskAsync("Keep a note: the plant needs water on Mondays.");

        Assert.Equal("Saved in /notes/plant.md.", answer);
        Assert.Contains(model.Tools, t => t.Name == "memory_write");
        var memory = MemoryServices.On(new LocalFolderStore(_folder));
        var owner = new LocalFolderStore(_folder).Owner;
        Assert.Equal(["Water on Mondays."], (await memory.ReadAsync(owner, "/notes/plant.md", null, null, CancellationToken.None)).Value!.Lines);
        Assert.Contains(await memory.EventsAsync(owner, new EventQuery(), CancellationToken.None), e => e.Kind == MemoryEventKind.Said && e.Text.Contains("Mondays"));
    }
}
