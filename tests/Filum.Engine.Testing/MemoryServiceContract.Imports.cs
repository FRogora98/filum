using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Imports;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    private const string Chat = "13/03/24, 09:16 - Sam: The keys are with the neighbour\n14/03/24, 18:02 - Alex: Thanks!\n14/03/24, 18:05 - Messages to this group are now secured.\n";

    [Fact]
    public async Task ImportAsync_WhenPreviewedThenImported_ShouldWriteNothingThenRecordEachNewItemOnceWithItsDate()
    {
        var preview = Ok(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat), None));

        Assert.Equal((ImportFormat.WhatsApp, 2, 2, 0, 1, false), (preview.Format, preview.Items, preview.New, preview.Duplicates, preview.Skipped, preview.Written));
        Assert.Equal(["Sam: The keys are with the neighbour", "Alex: Thanks!"], preview.Sample);
        Assert.Empty(await _memory.EventsAsync(_user, new EventQuery(), None));

        var imported = Ok(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Preview: false), None));
        var again = Ok(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Preview: false), None));

        Assert.True(imported.Written);
        Assert.Equal((0, 2, false), (again.New, again.Duplicates, again.Written));
        var events = await _memory.EventsAsync(_user, new EventQuery(), None);
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal((MemoryEventKind.Imported, MemoryEventSource.Import, (Guid?)imported.SourceId), (e.Kind, e.Source, e.ConversationId)));
        Assert.Equal(new DateTimeOffset(2024, 3, 13, 9, 16, 0, TimeSpan.Zero), events[0].OccurredAt);
        Assert.Equal(imported.SourceId, preview.SourceId);
    }

    [Fact]
    public async Task ImportAsync_WhenPrivate_ShouldBeFoundOnlyWhenAskedAndForgottenWholeWithItsSource()
    {
        var imported = Ok(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Sensitivity: MemorySensitivity.Private, Preview: false), None));

        Assert.Empty(Ok(await _memory.SearchEventsAsync(_user, "neighbour keys", null, null, includePrivate: false, None)));
        Assert.Single(Ok(await _memory.SearchEventsAsync(_user, "neighbour keys", null, null, includePrivate: true, None)));

        var forgotten = await _memory.ForgetConversationAsync(_user, imported.SourceId, None);

        Assert.Equal(2, forgotten.Events);
        Assert.DoesNotContain(await _memory.EventsAsync(_user, new EventQuery(), None), e => e.Kind == MemoryEventKind.Imported);
    }

    [Fact]
    public async Task RecordItemsAsync_WhenAnItemIsDatedInAnotherTimeZone_ShouldKeepTheSameInstant()
    {
        var at = new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.FromHours(2));

        Ok(await _memory.RecordItemsAsync(_user, Guid.NewGuid(), MemoryEventSource.Connector, [new ImportItem(at, "Sam", "Mail from Sam · Plans", "zoned|1")], MemorySensitivity.Normal, None));

        Assert.Equal(at.UtcDateTime, Assert.Single(await _memory.EventsAsync(_user, new EventQuery(), None)).OccurredAt.UtcDateTime);
        Assert.Single(await _memory.EventsAsync(_user, new EventQuery(From: at.AddMinutes(-1), Before: at.AddMinutes(1)), None));
    }

    [Fact]
    public async Task RecordItemsAsync_WhenTheSensitivityIsUnknownOrTheItemsAreTooMany_ShouldRefuseAndRecordNothing()
    {
        var item = new ImportItem(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero), "Sam", "Sam: See you", "refused|1");
        var small = Memory(new MemoryOptions { MaxImportItems = 1 });

        Assert.Contains("normal, sensitive or private", Refused(await small.RecordItemsAsync(_user, Guid.NewGuid(), MemoryEventSource.Connector, [item], "secret", None)));
        Assert.Contains("At most 1 items", Refused(await small.RecordItemsAsync(_user, Guid.NewGuid(), MemoryEventSource.Connector, [item, item with { Identity = "refused|2" }], MemorySensitivity.Normal, None)));
        Assert.Empty(await small.EventsAsync(_user, new EventQuery(), None));
    }

    [Fact]
    public async Task RecordItemsAsync_WhenAFileImportAlreadyRecordedAnItem_ShouldSkipItAndCarryTheLiveSource()
    {
        var imported = Ok(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Preview: false), None));
        var fromFile = Importer.Parse(ImportFormat.WhatsApp, "WhatsApp Chat with Sam.txt", Chat, DateTimeOffset.UtcNow).Items;
        var connection = Guid.NewGuid();
        var later = new ImportItem(new DateTimeOffset(2024, 3, 15, 8, 0, 0, TimeSpan.Zero), "Sam", "Sam: Back on Friday", "live|1");

        var recorded = Ok(await _memory.RecordItemsAsync(_user, connection, MemoryEventSource.Connector, [.. fromFile, later], MemorySensitivity.Sensitive, None)).New;
        var again = Ok(await _memory.RecordItemsAsync(_user, connection, MemoryEventSource.Connector, [later], MemorySensitivity.Sensitive, None)).New;

        Assert.Equal((1, 0), (recorded, again));
        var live = (await _memory.EventsAsync(_user, new EventQuery(), None)).Single(e => e.ConversationId == connection);
        Assert.Equal((MemoryEventKind.Imported, MemoryEventSource.Connector, MemorySensitivity.Sensitive, "Sam: Back on Friday"), (live.Kind, live.Source, live.Sensitivity, live.Text));
        Assert.Equal(later.When, live.OccurredAt);
        Assert.Equal(1, (await _memory.ForgetConversationAsync(_user, connection, None)).Events);
        Assert.Equal(2, (await _memory.EventsAsync(_user, new EventQuery(), None)).Count(e => e.ConversationId == imported.SourceId));
    }

    [Fact]
    public async Task ImportAsync_WhenTheFileIsOverTheLimitsOrOfNoKnownFormat_ShouldWriteNothing()
    {
        var small = Memory(new MemoryOptions { MaxImportItems = 1 });

        Assert.Contains("split it", Refused(await small.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Preview: false), None)));
        Assert.Contains("not recognized", Refused(await _memory.ImportAsync(_user, new ImportRequest("notes.txt", "plain words\n", Preview: false), None)));
        Assert.Contains("normal, sensitive or private", Refused(await _memory.ImportAsync(_user, new ImportRequest("WhatsApp Chat with Sam.txt", Chat, Sensitivity: "secret"), None)));
        Assert.Empty(await _memory.EventsAsync(_user, new EventQuery(), None));
    }
}
