using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task RecordAsync_WhenEventsAreRecorded_ShouldKeepThemInOrderAndReadThemBackWithFilters()
    {
        var chat = Guid.NewGuid();
        var said = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "I moved to\r\nthe coast", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), chat, Guid.NewGuid()), None);
        var answered = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "Noted.", ConversationId: chat), None);
        var derived = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "Wrote 1 change", Sources: [said.Id], Revisions: [7]), None);

        Assert.True(said.Id < answered.Id && answered.Id < derived.Id);
        Assert.Equal("I moved to\nthe coast", said.Text);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), said.OccurredAt);
        Assert.True(answered.OccurredAt == answered.RecordedAt);

        var all = await _memory.EventsAsync(_user, new EventQuery(), None);
        Assert.Equal([said.Id, answered.Id, derived.Id], all.Select(e => e.Id));
        Assert.Equal([said.Id], all[2].Sources);
        Assert.Equal([7L], all[2].Revisions);
        Assert.Equal([answered.Id, derived.Id], (await _memory.EventsAsync(_user, new EventQuery(AfterId: said.Id), None)).Select(e => e.Id));
        Assert.Equal([said.Id, answered.Id], (await _memory.EventsAsync(_user, new EventQuery(ConversationId: chat), None)).Select(e => e.Id));
        Assert.Equal([derived.Id], (await _memory.EventsAsync(_user, new EventQuery(Kinds: [MemoryEventKind.Derived]), None)).Select(e => e.Id));
        Assert.Equal([said.Id], (await _memory.EventsAsync(_user, new EventQuery(Before: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)), None)).Select(e => e.Id));
    }

    [Fact]
    public async Task ListEventsAsync_WhenAnItemIsSaidInManyConversations_ShouldListEveryMentionOnceInDayOrderWithinTheCap()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 30; i++)
        {
            var text = i % 3 == 0 ? $"I fixed the old bookshelf, take {i}" : $"Bought another lamp for the hall, number {i}";
            await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, text, start.AddDays(30 - i), Guid.NewGuid()), None);
        }

        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "Both the lamps and a bookshelf, today", start.AddDays(40), Guid.NewGuid()), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "A lamp, noted.", start.AddDays(41)), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "my private lamp", start.AddDays(42), Sensitivity: MemorySensitivity.Private), None);

        var lamps = Ok(await _memory.ListEventsAsync(_user, "lamp", null, null, includePrivate: false, 100, None));
        var either = Ok(await _memory.ListEventsAsync(_user, "bookshelf | lamp", null, null, includePrivate: false, 100, None));
        var capped = Ok(await _memory.ListEventsAsync(_user, "bookshelf | lamp", null, null, includePrivate: false, 5, None));

        Assert.Equal(22, lamps.Count);
        Assert.Contains(lamps, e => e.Kind == MemoryEventKind.Answered);
        Assert.Equal(lamps.OrderBy(e => e.OccurredAt).Select(e => e.Id), lamps.Select(e => e.Id));
        Assert.Equal(32, either.Count);
        Assert.Equal(either.Count, either.Select(e => e.Id).Distinct().Count());
        Assert.DoesNotContain(either, e => e.Kind == MemoryEventKind.Derived || e.Sensitivity == MemorySensitivity.Private);
        Assert.Equal(either.Take(5).Select(e => e.Id), capped.Select(e => e.Id));
        Assert.Single(Ok(await _memory.ListEventsAsync(_user, "lamp", null, null, includePrivate: true, 100, None)), e => e.Sensitivity == MemorySensitivity.Private);
        Assert.Contains("between 1 and 200", Refused(await _memory.ListEventsAsync(_user, " ", null, null, false, 10, None)));
    }

    [Fact]
    public async Task SearchEventsAsync_WhenWordsMatch_ShouldRankWhatWasSaidAndGiveTheReplyAfterIt()
    {
        var chat = Guid.NewGuid();
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "The weather is fine today", ConversationId: chat), None);
        var asked = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "My bicycle is the blue one with a basket", ConversationId: chat), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "A blue bicycle, noted.", ConversationId: chat), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "bicycle bicycle bicycle"), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "my private bicycle code", Sensitivity: MemorySensitivity.Private), None);

        var hits = Ok(await _memory.SearchEventsAsync(_user, "Bicycle basket", null, null, includePrivate: false, None));

        Assert.Equal(asked.Id, hits[0].Event.Id);
        Assert.Equal("A blue bicycle, noted.", hits[0].Next?.Text);
        Assert.DoesNotContain(hits, h => h.Event.Kind == MemoryEventKind.Derived || h.Event.Sensitivity == MemorySensitivity.Private);
        Assert.Contains(Ok(await _memory.SearchEventsAsync(_user, "code", null, null, includePrivate: true, None)), h => h.Event.Sensitivity == MemorySensitivity.Private);
        Assert.Empty(Ok(await _memory.SearchEventsAsync(_user, "bicycle", DateTimeOffset.UtcNow.AddDays(1), null, false, None)));
        Assert.Contains("between 1 and 200", Refused(await _memory.SearchEventsAsync(_user, " ", null, null, false, None)));
    }

    [Fact]
    public async Task ForgetAsync_WhenEventsAndFilesAreForgotten_ShouldRemoveThemWholeForGood()
    {
        var kept = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "keep this"), None);
        var gone = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "forget this"), None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/keep.md", "keep\n", None));
        var dropped = Ok(await _memory.WriteAsync(_user, Agent, "/notes/drop.md", "drop\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/drop.md", "more", None));
        await _memory.ForgetAsync(_user, [gone.Id], ["/notes/drop.md"], None);

        Assert.Equal([kept.Id], (await _memory.EventsAsync(_user, new EventQuery(), None)).Select(e => e.Id));
        Assert.Equal(["/notes/keep.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
        Assert.Contains("no change", Refused(await _memory.GetRevisionAsync(_user, dropped.RevisionId, None)));
        var next = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "after"), None);
        Assert.True(next.Id > gone.Id);
    }

    [Fact]
    public async Task AppendEventsAsync_WhenManyEventsComeAtOnce_ShouldAppendThemInOrder()
    {
        var first = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "before"), None);
        var batch = await StoreOf(_memory).AppendEventsAsync(_user,
            [new NewMemoryEvent(MemoryEventKind.Imported, MemoryEventSource.Import, "one"), new NewMemoryEvent(MemoryEventKind.Imported, MemoryEventSource.Import, "two")],
            DateTimeOffset.UtcNow, None);

        Assert.Equal(["one", "two"], batch.Select(e => e.Text));
        Assert.True(batch[0].Id > first.Id && batch[1].Id > batch[0].Id);
        Assert.Equal(["before", "one", "two"], (await _memory.EventsAsync(_user, new EventQuery(), None)).Select(e => e.Text));
    }

    [Fact]
    public async Task SaveVectorsAsync_WhenAnEventIsForgotten_ShouldKeepVectorsByEventAndForgetItsOwn()
    {
        var kept = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "kept"), None);
        var gone = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "gone"), None);
        var store = StoreOf(_memory);

        await store.SaveVectorsAsync(_user, [(kept.Id, [1f, 0f]), (gone.Id, [0f, 1f]), (kept.Id + 1_000_000, [1f, 1f])], None);
        await store.SaveVectorsAsync(_user, [(kept.Id, [0.5f, 0.5f])], None);

        var vectors = await store.VectorsAsync(_user, None);
        Assert.Equal([kept.Id, gone.Id], vectors.Keys.Order());
        Assert.Equal([0.5f, 0.5f], vectors[kept.Id]);
        Assert.Empty(await store.VectorsAsync(Guid.NewGuid(), None));

        await store.ForgetAsync(_user, [gone.Id], [], None);

        Assert.Equal([kept.Id], (await store.VectorsAsync(_user, None)).Keys);
    }

    [Fact]
    public async Task EventsAsync_WhenAnotherPersonAsks_ShouldFindNoEvents()
    {
        var other = Guid.NewGuid();
        var mine = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "mine"), None);

        Assert.Empty(await _memory.EventsAsync(other, new EventQuery(), None));
        Assert.Empty(Ok(await _memory.SearchEventsAsync(other, "mine", null, null, true, None)));
        await _memory.ForgetAsync(other, [mine.Id], [], None);
        Assert.Single(await _memory.EventsAsync(_user, new EventQuery(), None));

        if (OnePersonPerStore)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _memory.RecordAsync(other, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "theirs"), None));
        }
    }
}
