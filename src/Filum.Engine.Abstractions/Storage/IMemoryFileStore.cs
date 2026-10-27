using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Abstractions.Storage;

/// <summary>The current state of a person's memory files: finding, listing and saving them, one user at a time.</summary>
public interface IMemoryFileStore
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

    /// <summary>How many live files the person has.</summary>
    Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether a live file other than <paramref name="exceptFileId"/> is at <paramref name="path"/>.</summary>
    Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a change atomically: the file's new state and one new revision. <paramref name="current"/> is the file as it
    /// was read (null for a new file); when the stored file is no longer at that version, or another live file took the
    /// path meanwhile, nothing is saved and the result is null: the change lost a race.
    /// </summary>
    Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken);
}
