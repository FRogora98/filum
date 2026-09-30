namespace Filum.Engine;

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
    public bool IsLive => DeletedAt is null;
}

/// <summary>One change of one file, as it was saved: the whole content after the change, and who made it.</summary>
public sealed record StoredRevision(
    long Id,
    Guid FileId,
    string Path,
    string Content,
    string Sensitivity,
    bool Deleted,
    string Operation,
    string Author,
    Guid? ConversationId,
    Guid? MessageId,
    long? UndoesRevisionId,
    DateTimeOffset CreatedAt);

/// <summary>What a file becomes after a change, as the engine decided it; the store only persists it.</summary>
public sealed record FileState(string Path, string Content, string Sensitivity, bool Deleted, int SizeBytes, int LineCount, string? Header, int? RowCount);

/// <summary>A saved change: the revision it produced and the file it belongs to.</summary>
public sealed record SavedChange(long RevisionId, Guid FileId);

/// <summary>
/// Where a person's memory is persisted. The rules (paths, checks, collections, skills, undo) are the engine's, in
/// <see cref="MemoryService"/>; a store only reads and writes what it is told, always for one user: another user's
/// file or revision does not exist. Every method is safe to call concurrently.
/// </summary>
public interface IMemoryStore
{
    /// <summary>The live file at <paramref name="path"/>, or null.</summary>
    Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken);

    /// <summary>A file by id, deleted or not, or null.</summary>
    Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    /// <summary>
    /// The live files under <paramref name="prefix"/> (every file for null or "/"), private ones only when asked, in
    /// no particular order. Without <paramref name="withContent"/> the content may be left empty.
    /// </summary>
    Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken);

    /// <summary>The live files under <paramref name="prefix"/> whose content contains <paramref name="text"/>, ignoring case.</summary>
    Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken);

    Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether a live file other than <paramref name="exceptFileId"/> is at <paramref name="path"/>.</summary>
    Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken);

    Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken);

    /// <summary>Every revision of a file, oldest first.</summary>
    Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    /// <summary>The revisions among <paramref name="revisionIds"/> that exist for this user.</summary>
    Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);

    /// <summary>Which of these revisions were taken back by an undo.</summary>
    Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a change atomically: the file's new state and one new revision. <paramref name="current"/> is the file as it
    /// was read (null for a new file); when the stored file is no longer at that version, or another live file took the
    /// path meanwhile, nothing is saved and the result is null: the change lost a race.
    /// </summary>
    Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The titles of the person's conversations among these ids; a store without conversations returns none.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken);
}
