using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Memory.History;

internal sealed class MemoryHistoryService(IMemoryStore store)
{
    public const int ChangedLinesRevisions = 20;

    public async Task<IReadOnlyDictionary<string, LineChange>> LineChangesAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        var live = MemoryPaths.Validate(path) is null ? await store.FindLiveAsync(userId, path, cancellationToken) : null;
        if (live is null)
        {
            return new Dictionary<string, LineChange>();
        }

        var revisions = await store.RevisionsOfFileAsync(userId, live.Id, cancellationToken);
        return LineChanges.Of(revisions.Where(r => !r.Deleted).TakeLast(ChangedLinesRevisions + 1).Select(r => (r.Content, r.CreatedAt)).ToList());
    }

    public async Task<MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>> GetHistoryAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Refused(error);
        }

        var live = await store.FindLiveAsync(userId, path, cancellationToken);
        if (live is null)
        {
            return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Missing(FileRefusals.NoFileAt(path));
        }

        var revisions = await store.RevisionsOfFileAsync(userId, live.Id, cancellationToken);
        var titles = await TitlesAsync(userId, revisions.Select(r => r.ConversationId), cancellationToken);

        var history = new List<MemoryRevisionInfo>();
        StoredRevision? previous = null;
        foreach (var r in revisions)
        {
            history.Add(new MemoryRevisionInfo(r.Id, r.Path, r.Operation, r.Author, r.ConversationId, r.MessageId, r.Deleted, r.Sensitivity, r.UndoesRevisionId, r.CreatedAt,
                RevisionSummary.Of(r, previous), r.ConversationId is { } id ? titles.GetValueOrDefault(id) : null));
            previous = r;
        }

        history.Reverse();
        return MemoryOutcome<IReadOnlyList<MemoryRevisionInfo>>.Ok(history);
    }

    public async Task<MemoryOutcome<MemoryVersion>> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken)
    {
        var version = await store.GetRevisionAsync(userId, revisionId, cancellationToken) is { } r
            ? new MemoryVersion(r.Id, r.Path, r.Content, r.Deleted, r.CreatedAt)
            : null;
        return version is null ? MemoryOutcome<MemoryVersion>.Missing(FileRefusals.NoChange(revisionId)) : MemoryOutcome<MemoryVersion>.Ok(version);
    }

    public async Task<MemoryOutcome<MemoryOrigin>> GetOriginAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<MemoryOrigin>.Refused(error);
        }

        var live = await store.FindLiveAsync(userId, path, cancellationToken);
        if (live is null)
        {
            return MemoryOutcome<MemoryOrigin>.Missing(FileRefusals.NoFileAt(path));
        }

        var first = (await store.RevisionsOfFileAsync(userId, live.Id, cancellationToken))[0];
        var titles = await TitlesAsync(userId, [first.ConversationId], cancellationToken);
        return MemoryOutcome<MemoryOrigin>.Ok(new MemoryOrigin(first.Author, first.ConversationId is { } id ? titles.GetValueOrDefault(id) : null, first.CreatedAt));
    }

    public async Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return new HashSet<long>();
        }

        return await store.UndoneAmongAsync(userId, revisionIds, cancellationToken);
    }

    public async Task<string?> LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        var file = await store.FindLiveAsync(userId, path, cancellationToken);
        return file is null ? null : (await store.GetRevisionAsync(userId, file.LatestRevisionId, cancellationToken))?.Author;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> TitlesAsync(Guid userId, IEnumerable<Guid?> conversationIds, CancellationToken cancellationToken)
    {
        var ids = conversationIds.OfType<Guid>().Distinct().ToList();
        return ids.Count == 0 ? new Dictionary<Guid, string>() : await store.ConversationTitlesAsync(userId, ids, cancellationToken);
    }
}
