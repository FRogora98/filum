using Filum.Engine.Abstractions.Events;
using Filum.Engine.Memory.Contracts;

namespace Filum.Engine.Tools;

// The messages an event was said among: its conversation, or, for an event outside one (an import, a message logged on
// its own), the others of its day outside a conversation.
internal static class Conversations
{
    public static readonly string[] SpokenKinds = [MemoryEventKind.Said, MemoryEventKind.Answered, MemoryEventKind.Imported];

    // The same key for every event of the same conversation, or of the same day outside one.
    public static string KeyOf(MemoryEvent e) =>
        e.ConversationId is { } id ? id.ToString("N") : e.OccurredAt.UtcDateTime.ToString(EngineConstants.Dates.Day, System.Globalization.CultureInfo.InvariantCulture);

    public static async Task<List<MemoryEvent>> OfAsync(IMemoryEvents log, Guid userId, MemoryEvent found, bool withPrivate, CancellationToken cancellationToken)
    {
        var day = new DateTimeOffset(found.OccurredAt.UtcDateTime.Date, TimeSpan.Zero);
        var query = found.ConversationId is { } conversation
            ? new EventQuery(Kinds: SpokenKinds, ConversationId: conversation, IncludePrivate: withPrivate)
            : new EventQuery(Kinds: SpokenKinds, From: day, Before: day.AddDays(1), IncludePrivate: withPrivate);
        return (await log.EventsAsync(userId, query, cancellationToken))
            .Where(e => found.ConversationId is not null || e.ConversationId is null)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .ToList();
    }

    // One event by its number, or null when there is none (or it is private and private ones are not asked).
    public static async Task<MemoryEvent?> EventAsync(IMemoryEvents log, Guid userId, long id, bool withPrivate, CancellationToken cancellationToken) =>
        (await log.EventsAsync(userId, new EventQuery(AfterId: id - 1, Kinds: SpokenKinds, IncludePrivate: withPrivate), cancellationToken))
            .FirstOrDefault(e => e.Id == id);
}
