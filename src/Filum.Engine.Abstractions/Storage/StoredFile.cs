namespace Filum.Engine.Abstractions.Storage;

/// <summary>A memory file as a store keeps it: its current state, deleted or not.</summary>
public sealed record StoredFile(
    Guid Id,
    string Path,
    string Content,
    string Sensitivity,
    int SizeBytes,
    int LineCount,
    string? Header,
    int? RowCount,
    int Version,
    long LatestRevisionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt)
{
    /// <summary>Whether the file exists now, as opposed to deleted and kept in the history.</summary>
    public bool IsLive => DeletedAt is null;
}
