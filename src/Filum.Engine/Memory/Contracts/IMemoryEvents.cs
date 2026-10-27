using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// A person's log of events: recording, reading and searching what was said, answered or imported. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryEvents
{
    /// <summary>Appends one event; the text is kept as it was given, line breaks normalized.</summary>
    Task<MemoryEvent> RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken);

    /// <summary>The person's events that match <paramref name="query"/>, oldest first.</summary>
    Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken);

    /// <summary>The titles of the person's conversations among these ids, for showing where an event was said.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken);

    /// <summary>
    /// The events that best match <paramref name="query"/> among what was said, answered or imported, in the date window,
    /// each with the event that followed it in its conversation.
    /// </summary>
    Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> SearchEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken);

    /// <summary>
    /// Every message said that mentions <paramref name="query"/> or one of its alternatives (separated by <c>|</c>), with
    /// the best matches of a normal search, in the date window, oldest first, at most <paramref name="max"/> (spec 057).
    /// </summary>
    Task<MemoryOutcome<IReadOnlyList<MemoryEvent>>> ListEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, int max, CancellationToken cancellationToken);

    /// <summary>Makes the missing vectors of what was said, newest first, at most <paramref name="max"/>; returns how many it made.</summary>
    Task<int> MakeVectorsAsync(Guid userId, int max, CancellationToken cancellationToken);
}
