using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.UnitTests.Fakes;

namespace Filum.Engine.UnitTests.Events;

public sealed class EventVectorServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly InMemoryMemoryStore _store = new();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task MakeVectorsAsync_WhenASearchMadeAFew_ShouldMakeTheRest()
    {
        var memory = Memory(new FamilyEmbedder(), new MemoryOptions { VectorsPerSearch = 2 });
        for (var i = 0; i < 5; i++)
        {
            await memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, $"note {i}"), None);
        }

        await memory.SearchEventsAsync(_user, "note", null, null, false, None);
        Assert.Equal(2, (await _store.VectorsAsync(_user, None)).Count);

        Assert.Equal(3, await memory.MakeVectorsAsync(_user, 100, None));
        Assert.Equal(0, await memory.MakeVectorsAsync(_user, 100, None));
        Assert.Equal(0, await Memory(null).MakeVectorsAsync(_user, 100, None));
    }

    private IMemoryService Memory(IEmbedder? embedder, MemoryOptions? options = null) =>
        MemoryServices.On(_store, options, embedder: embedder);
}
