using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Storage.LocalFolder;

// One line of events.jsonl. The disk's own record, apart from the public model: its fields, their names and their order
// are the format every version since 0.7 writes and reads.
internal sealed record EventLine(
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
    IReadOnlyList<long> Revisions)
{
    public static EventLine From(MemoryEvent stored) =>
        new(stored.Id, stored.OccurredAt, stored.RecordedAt, stored.Kind, stored.Source, stored.ConversationId, stored.MessageId,
            stored.Text, stored.Sensitivity, stored.Sources, stored.Revisions);

    public MemoryEvent ToEvent() =>
        new(Id, OccurredAt, RecordedAt, Kind, Source, ConversationId, MessageId, Text, Sensitivity, Sources, Revisions);
}
