namespace Filum.Agent;

/// <summary>
/// The current state of one file of a person's memory. The id is stable across moves; a delete only sets
/// <see cref="DeletedAt"/>, so the file can be brought back from its history.
/// </summary>
public sealed class MemoryFile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = MemorySensitivity.Normal;

    public int SizeBytes { get; set; }

    public int LineCount { get; set; }

    /// <summary>The header line of a collection; null for documents.</summary>
    public string? Header { get; set; }

    /// <summary>The records after the header of a collection; null for documents.</summary>
    public int? RowCount { get; set; }

    public long LatestRevisionId { get; set; }

    /// <summary>Optimistic concurrency token: two changes from the same version cannot both win.</summary>
    public int Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
