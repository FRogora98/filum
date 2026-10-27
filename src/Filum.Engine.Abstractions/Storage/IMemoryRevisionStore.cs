namespace Filum.Engine.Abstractions.Storage;

/// <summary>The history of a person's memory files: every revision as it was saved, read back for history and undo.</summary>
public interface IMemoryRevisionStore
{
    /// <summary>One revision by id, or null.</summary>
    Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken);

    /// <summary>Every revision of a file, oldest first.</summary>
    Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    /// <summary>The revisions among <paramref name="revisionIds"/> that exist for this user.</summary>
    Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);

    /// <summary>Which of these revisions were taken back by an undo.</summary>
    Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken);
}
