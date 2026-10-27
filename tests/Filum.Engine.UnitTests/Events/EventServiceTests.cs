using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.UnitTests.Fakes;

namespace Filum.Engine.UnitTests.Events;

// The fake model's "meaning" is a few word families.
public sealed class EventServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly InMemoryMemoryStore _store = new();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task SearchEventsAsync_WhenThereIsAModel_ShouldFindOtherWordsAndWithoutOneMatchTheWordsAlone()
    {
        var words = Memory(null);
        await words.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "I bought a fern for the balcony"), None);
        await words.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "The train was late again"), None);

        Assert.Empty((await words.SearchEventsAsync(_user, "plants", null, null, false, None)).Value!);

        var meaning = Memory(new FamilyEmbedder());
        var found = (await meaning.SearchEventsAsync(_user, "plants", null, null, false, None)).Value!;

        Assert.Equal("I bought a fern for the balcony", found[0].Event.Text);
        Assert.Equal(2, (await _store.VectorsAsync(_user, None)).Count);
    }

    private IMemoryService Memory(IEmbedder? embedder, MemoryOptions? options = null) =>
        MemoryServices.On(_store, options, embedder: embedder);
}
