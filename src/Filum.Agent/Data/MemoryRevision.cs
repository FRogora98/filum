namespace Filum.Agent;

/// <summary>One state a memory file has had, with who made the change and how. Revisions are only ever added.</summary>
public sealed class MemoryRevision
{
    public long Id { get; set; }

    public Guid FileId { get; set; }

    public Guid UserId { get; set; }

    public string Path { get; set; } = string.Empty;

    /// <summary>The content after the change.</summary>
    public string Content { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = MemorySensitivity.Normal;

    /// <summary>This revision deleted the file.</summary>
    public bool Deleted { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public Guid? ConversationId { get; set; }

    /// <summary>The person's message of the turn that made the change.</summary>
    public Guid? MessageId { get; set; }

    public long? UndoesRevisionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
