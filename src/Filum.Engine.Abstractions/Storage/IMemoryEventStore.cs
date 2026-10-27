using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Abstractions.Storage;

/// <summary>A person's log of events: appended to, read in order, never rewritten.</summary>
public interface IMemoryEventStore
{
    /// <summary>Appends one event to the person's log, with the next id.</summary>
    Task<MemoryEvent> AppendEventAsync(Guid userId, NewMemoryEvent next, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Appends many events at once (an import), in order, with consecutive ids.</summary>
    Task<IReadOnlyList<MemoryEvent>> AppendEventsAsync(Guid userId, IReadOnlyList<NewMemoryEvent> next, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The person's events that match <paramref name="query"/>, oldest first.</summary>
    Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken);
}
