namespace Filum.Engine.Abstractions.Storage;

/// <summary>
/// Where a person's memory is persisted: every role of a store together. The rules (paths, checks, collections,
/// skills, undo) are the engine's, in <c>IMemoryService</c>; a store only reads and writes what it is told,
/// always for one user: another user's file or revision does not exist. Every method is safe to call concurrently.
/// </summary>
public interface IMemoryStore : IMemoryFileStore, IMemoryRevisionStore, IMemoryEventStore, IMemoryVectorStore, IConversationDirectory
{
    /// <summary>
    /// Forgets for good: removes these events (and their vectors), and these files with every revision of theirs. The one exception to the
    /// append-only rule, for the person's right to be forgotten. It spans every role, so it belongs to the whole store.
    /// </summary>
    Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken);
}
