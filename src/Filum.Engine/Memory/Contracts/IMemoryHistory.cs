using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// The history of a person's files: revisions, versions and origins, and taking changes back. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryHistory
{
    /// <summary>The lines of the file at <paramref name="path"/> that replaced others in its last revisions, with what they said before.</summary>
    Task<IReadOnlyDictionary<string, LineChange>> LineChangesAsync(Guid userId, string path, CancellationToken cancellationToken);

    /// <summary>The history of the file now at <paramref name="path"/>, newest first.</summary>
    Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <summary>One version of a file, for reading it before restoring it.</summary>
    Task<MemoryOutcome<MemoryVersion>> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken);

    /// <summary>Where the live file at <paramref name="path"/> came from: the conversations and messages that changed it.</summary>
    Task<MemoryOutcome<MemoryOrigin>> GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <summary>Which of these revisions were taken back by an undo.</summary>
    Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);

    /// <summary>Who made the latest change of the live file at <paramref name="path"/>, or null when there is none.</summary>
    Task<string?> LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken);

    /// <summary>
    /// Brings back the version of <paramref name="revisionId"/>, as a new revision by <paramref name="actor"/>. Unlike an
    /// undo it is allowed after later changes: the person picked this exact version.
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken);

    /// <summary>Takes back the change of one revision, as a new revision; refused when the file changed again afterwards.</summary>
    Task<MemoryOutcome<MemoryChange>> UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes back the changes of a failed turn: every file it touched returns to its state before the turn, as one
    /// rollback revision per file. A file changed by someone else after the turn is kept as it is, and logged.
    /// </summary>
    Task RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken);
}
