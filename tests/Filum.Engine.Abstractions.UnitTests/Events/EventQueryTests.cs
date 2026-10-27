using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Abstractions.UnitTests.Events;

public sealed class EventQueryTests
{
    private static readonly Guid Chat = Guid.NewGuid();
    private static readonly MemoryEvent Said = Event(1, MemoryEventKind.Said, new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), Chat, MemorySensitivity.Normal);
    private static readonly MemoryEvent Secret = Event(2, MemoryEventKind.Said, new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero), null, MemorySensitivity.Private);
    private static readonly MemoryEvent Derived = Event(3, MemoryEventKind.Derived, new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero), Chat, MemorySensitivity.Normal);
    private static readonly MemoryEvent[] All = [Said, Secret, Derived];

    [Fact]
    public void Matches_WhenTheQueryIsEmpty_ShouldMatchEveryEventPrivateOnesIncluded()
    {
        Assert.Equal([1L, 2L, 3L], Ids(new EventQuery()));
    }

    [Fact]
    public void Matches_WhenFiltered_ShouldKeepOnlyTheEventsOfEveryFilter()
    {
        Assert.Equal([2L, 3L], Ids(new EventQuery(AfterId: 1)));
        Assert.Equal([3L], Ids(new EventQuery(Kinds: [MemoryEventKind.Derived])));
        Assert.Equal([1L, 3L], Ids(new EventQuery(ConversationId: Chat)));
        Assert.Equal([2L], Ids(new EventQuery(From: Secret.OccurredAt, Before: Derived.OccurredAt)));
        Assert.Equal([1L, 3L], Ids(new EventQuery(IncludePrivate: false)));
        Assert.Equal([3L], Ids(new EventQuery(AfterId: 1, IncludePrivate: false)));
    }

    private static IEnumerable<long> Ids(EventQuery query) => All.Where(query.Matches).Select(e => e.Id);

    private static MemoryEvent Event(long id, string kind, DateTimeOffset at, Guid? conversation, string sensitivity) =>
        new(id, at, at, kind, MemoryEventSource.Chat, conversation, null, "text", sensitivity, [], []);
}
