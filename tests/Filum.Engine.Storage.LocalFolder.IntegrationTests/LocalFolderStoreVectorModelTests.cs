using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// Spec 039: vectors are compared only with vectors of the same model.
public sealed class LocalFolderStoreVectorModelTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _person = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public async Task SearchEventsAsync_WhenTheEmbedderIsAnotherModel_ShouldMakeTheVectorsAgainWithIt()
    {
        await SaidAndSearchedAsync(new OneVectorEmbedder("model-a", 1f));

        await Open(new OneVectorEmbedder("model-b", 2f)).SearchEventsAsync(_person, "note", null, null, false, None);

        Assert.Equal("model-b", File.ReadAllText(Disk(".filum/vectors.model")).Trim());
        var vectors = await new LocalFolderStore(_folder, _person).VectorsAsync(_person, None);
        Assert.Equal(2, vectors.Count);
        Assert.All(vectors.Values, v => Assert.Equal(2f, v[0]));
    }

    [Fact]
    public async Task SearchEventsAsync_WhenTheFolderDoesNotSayWhichModelMadeItsVectors_ShouldKeepThemAndWriteIt()
    {
        await SaidAndSearchedAsync(new OneVectorEmbedder(null, 1f));
        Assert.False(File.Exists(Disk(".filum/vectors.model")));

        await Open(new OneVectorEmbedder("model-a", 2f)).SearchEventsAsync(_person, "note", null, null, false, None);

        Assert.Equal("model-a", File.ReadAllText(Disk(".filum/vectors.model")).Trim());
        Assert.All((await new LocalFolderStore(_folder, _person).VectorsAsync(_person, None)).Values, v => Assert.Equal(1f, v[0]));
    }

    [Fact]
    public async Task SearchEventsAsync_WhenTheModelIsTheSame_ShouldKeepItsVectors()
    {
        await SaidAndSearchedAsync(new OneVectorEmbedder("model-a", 1f));

        await Open(new OneVectorEmbedder("model-a", 2f)).SearchEventsAsync(_person, "note", null, null, false, None);

        Assert.All((await new LocalFolderStore(_folder, _person).VectorsAsync(_person, None)).Values, v => Assert.Equal(1f, v[0]));
    }

    // Two notes said, and a search that made their vectors.
    private async Task SaidAndSearchedAsync(IEmbedder embedder)
    {
        var memory = Open(embedder);
        await memory.RecordAsync(_person, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "first note"), None);
        await memory.RecordAsync(_person, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "second note"), None);
        await memory.SearchEventsAsync(_person, "note", null, null, false, None);
    }

    private IMemoryService Open(IEmbedder embedder) => MemoryServices.On(new LocalFolderStore(_folder, _person), embedder: embedder);

    private string Disk(string relative) => Path.Combine(_folder, relative.Replace('/', Path.DirectorySeparatorChar));

    // Every text gets the same vector, whose first value tells which embedder made it.
    private sealed class OneVectorEmbedder(string? id, float mark) : IEmbedder
    {
        public string? ModelId => id;

        public IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query) => texts.Select(_ => new[] { mark, 0f }).ToList();
    }
}
