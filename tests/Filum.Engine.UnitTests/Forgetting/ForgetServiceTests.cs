using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.UnitTests.Fakes;

namespace Filum.Engine.UnitTests.Forgetting;

public sealed class ForgetServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task ForgetConversationAsync_WhenAFactIsRecordedMeanwhile_ShouldKeepIt()
    {
        var user = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var store = BeforeFactsReadStore.Wrap(new InMemoryMemoryStore());
        var memory = MemoryServices.On(store);
        var said = await memory.RecordAsync(user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "Sam moved to Turin", ConversationId: conversation), None);
        var other = await memory.RecordAsync(user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.App, "I run on Sundays", ConversationId: Guid.NewGuid()), None);
        Assert.False((await memory.RecordFactAsync(user, MemoryActor.Person, "Sam", "city", "Turin", null, null, [said.Id], None)).IsRefused);

        // Between the forget's first look at the facts and its change, another turn records a fact.
        ((BeforeFactsReadStore)(object)store).Once = () => memory.RecordFactAsync(user, MemoryActor.Person, "me", "sport", "running", null, null, [other.Id], None);
        var forgotten = await memory.ForgetConversationAsync(user, conversation, None);

        Assert.Equal(1, forgotten.FactRows);
        var facts = (await memory.CurrentFactsAsync(user, null, None)).Value!;
        Assert.Equal(["running"], facts.Select(f => f.Value));
    }
}
