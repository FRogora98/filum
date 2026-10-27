using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Abstractions.UnitTests.Events;

public sealed class MemoryEventsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_WhenTheEventHasNoDateOrLinks_ShouldDateItNowAndGiveItEmptyLinks()
    {
        var stored = MemoryEvents.From(7, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "hello"), Now);

        Assert.Equal((7L, Now, Now), (stored.Id, stored.OccurredAt, stored.RecordedAt));
        Assert.Equal((MemoryEventKind.Said, MemoryEventSource.Chat, "hello", MemorySensitivity.Normal), (stored.Kind, stored.Source, stored.Text, stored.Sensitivity));
        Assert.Empty(stored.Sources);
        Assert.Empty(stored.Revisions);
    }

    [Fact]
    public void From_WhenTheEventCarriesItsDateAndLinks_ShouldKeepThem()
    {
        var at = new DateTimeOffset(2024, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var conversation = Guid.NewGuid();

        var stored = MemoryEvents.From(8, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "wrote", at, conversation, Sources: [3], Revisions: [5]), Now);

        Assert.Equal((at, Now, (Guid?)conversation), (stored.OccurredAt, stored.RecordedAt, stored.ConversationId));
        Assert.Equal([3L], stored.Sources);
        Assert.Equal([5L], stored.Revisions);
    }
}
