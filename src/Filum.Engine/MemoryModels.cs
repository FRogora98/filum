namespace Filum.Engine;

/// <summary>Who makes a change: the agent in one turn, the person from the app, or the platform itself.</summary>
public sealed record MemoryActor(string Author, Guid? ConversationId = null, Guid? MessageId = null)
{
    public static MemoryActor Platform { get; } = new(MemoryAuthor.Platform);

    public static MemoryActor Person { get; } = new(MemoryAuthor.Person);

    public static MemoryActor Agent(Guid conversationId, Guid messageId) => new(MemoryAuthor.Agent, conversationId, messageId);
}

/// <summary>A value, or the reason it was refused. Expected refusals are results, not exceptions.</summary>
public sealed record MemoryOutcome<T>(T? Value, string? Refusal, bool IsMissing = false) where T : class
{
    public bool IsRefused => Refusal is not null;

    public static MemoryOutcome<T> Ok(T value) => new(value, null);

    public static MemoryOutcome<T> Refused(string reason) => new(null, reason);

    /// <summary>A refusal because the file or change does not exist for this person.</summary>
    public static MemoryOutcome<T> Missing(string reason) => new(null, reason, IsMissing: true);
}

public sealed record MemoryFileInfo(string Path, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount);

public sealed record MemoryFileContent(MemoryFileInfo Info, string Content);

public sealed record MemoryReadResult(string Path, string Sensitivity, int FromLine, int ToLine, int TotalLines, IReadOnlyList<string> Lines);

public sealed record MemorySearchHit(string Path, int Line, string Text);

public sealed record MemorySearchResult(IReadOnlyList<MemorySearchHit> Hits, bool Truncated);

/// <summary>A change that was applied: the revision it produced and what the file looks like now.</summary>
public sealed record MemoryChange(long RevisionId, string Path, string Operation, bool Created, int LineCount, int? RowCount, int Added, string? FromPath = null, string? Sensitivity = null);

public sealed record MemoryRevisionInfo(long Id, string Path, string Operation, string Author, Guid? ConversationId, Guid? MessageId, bool Deleted, string Sensitivity, long? UndoesRevisionId, DateTimeOffset CreatedAt, string Summary = "", string? ConversationTitle = null);

public sealed record MemoryVersion(long Id, string Path, string Content, bool Deleted, DateTimeOffset CreatedAt);

/// <summary>Where a file came from: its first revision, and the chat that made it when there was one and it still exists.</summary>
public sealed record MemoryOrigin(string Author, string? ConversationTitle, DateTimeOffset CreatedAt);
