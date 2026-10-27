namespace Filum.Engine.Abstractions.Events;

/// <summary>One event of a person's log: what happened, when, where it came from. Never rewritten.</summary>
public sealed record MemoryEvent(
    long Id,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    string Kind,
    string Source,
    Guid? ConversationId,
    Guid? MessageId,
    string Text,
    string Sensitivity,
    IReadOnlyList<long> Sources,
    IReadOnlyList<long> Revisions);
