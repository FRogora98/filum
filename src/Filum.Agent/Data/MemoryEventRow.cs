namespace Filum.Agent;

/// <summary>One event of a person's log (spec 030), as Postgres keeps it. Rows are only ever added, or forgotten.</summary>
public sealed class MemoryEventRow
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public Guid? ConversationId { get; set; }

    public Guid? MessageId { get; set; }

    public string Text { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = MemorySensitivity.Normal;

    /// <summary>The events this one came from.</summary>
    public long[] Sources { get; set; } = [];

    /// <summary>The revisions this event made.</summary>
    public long[] Revisions { get; set; } = [];
}
