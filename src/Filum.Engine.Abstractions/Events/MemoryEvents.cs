namespace Filum.Engine.Abstractions.Events;

/// <summary>Helpers for stores.</summary>
public static class MemoryEvents
{
    /// <summary>The event a store saves for <paramref name="next"/>, with its id and recording time.</summary>
    public static MemoryEvent From(long id, NewMemoryEvent next, DateTimeOffset now) =>
        new(id, next.OccurredAt ?? now, now, next.Kind, next.Source, next.ConversationId, next.MessageId, next.Text, next.Sensitivity,
            next.Sources ?? [], next.Revisions ?? []);
}
