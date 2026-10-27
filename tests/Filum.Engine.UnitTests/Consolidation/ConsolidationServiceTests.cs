using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Consolidation;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.UnitTests.Fakes;

namespace Filum.Engine.UnitTests.Consolidation;

public sealed class ConsolidationServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly InMemoryMemoryStore _store = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory;

    public ConsolidationServiceTests()
    {
        _memory = MemoryServices.On(_store);
    }

    [Fact]
    public async Task PendingAsync_WhenAPassWasClosed_ShouldGiveTheEventsAfterIt()
    {
        var first = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "one"), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "Changed /a.md"), None);
        var second = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "two"), None);

        Assert.Equal([first.Id, second.Id], (await _memory.PendingAsync(_user, 10, None)).Select(e => e.Id));
        Assert.Equal([first.Id], (await _memory.PendingAsync(_user, 1, None)).Select(e => e.Id));

        await _memory.CloseConsolidationAsync(_user, [first.Id], [], None);
        Assert.Equal([second.Id], (await _memory.PendingAsync(_user, 10, None)).Select(e => e.Id));
        await _memory.CloseConsolidationAsync(_user, [second.Id], [], None);
        Assert.Empty(await _memory.PendingAsync(_user, 10, None));
        Assert.Contains("[event", ConsolidationPrompt.Prompt([first]));
    }

    [Fact]
    public async Task PendingAsync_WhenAnEventIsPrivate_ShouldNeverHandItToAPass()
    {
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "open"), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "secret", Sensitivity: MemorySensitivity.Private), None);

        Assert.Equal(["open"], (await _memory.PendingAsync(_user, 10, None)).Select(e => e.Text));
    }

    [Fact]
    public async Task GateAsync_WhenThereIsAModelAndAGate_ShouldHandAPassOnlyWhatIsAboutThePerson()
    {
        var gated = Memory(new WordEmbedder(), new FirstDimensionGate());
        var mine = await gated.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "my sister moved to Lisbon"), None);
        await gated.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "rewrite this paragraph in a formal tone"), None);
        var pending = await gated.PendingAsync(_user, 10, None);

        Assert.Equal([mine.Id], (await gated.GateAsync(_user, pending, None)).Select(e => e.Id));
        Assert.Equal(2, (await Memory(null, new FirstDimensionGate()).GateAsync(_user, pending, None)).Count);
        Assert.Equal(2, (await Memory(new WordEmbedder(), null).GateAsync(_user, pending, None)).Count);
    }

    private IMemoryService Memory(IEmbedder? embedder, IEventGate? gate) =>
        MemoryServices.On(_store, embedder: embedder, gate: gate);
}
