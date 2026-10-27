using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.InMemory;

/// <summary>
/// A memory held in the process, for tests, previews and short-lived tools: the same contract as a real store
/// (per-user, versions checked, one live file per path, atomic saves), with nothing persisted.
/// </summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, (Guid UserId, StoredFile File)> _files = [];
    private readonly List<(Guid UserId, StoredRevision Revision)> _revisions = [];
    private readonly List<(Guid UserId, MemoryEvent Event)> _events = [];
    private readonly Dictionary<(Guid UserId, long EventId), float[]> _vectors = [];
    private long _nextRevision = 1;
    private long _nextEvent = 1;

    /// <inheritdoc/>
    public Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).FirstOrDefault(f => f.IsLive && f.Path == path)));

    /// <inheritdoc/>
    public Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).FirstOrDefault(f => f.Id == fileId)));

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredFile>>(Read(() => Visible(userId, prefix, includePrivate).ToList()));

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredFile>>(Read(() => Visible(userId, prefix, includePrivate).Where(f => f.Content.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList()));

    /// <inheritdoc/>
    public Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).Count(f => f.IsLive)));

    /// <inheritdoc/>
    public Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).Any(f => f.IsLive && f.Path == path && f.Id != exceptFileId)));

    /// <inheritdoc/>
    public Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Revisions(userId).FirstOrDefault(r => r.Id == revisionId)));

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredRevision>>(Read(() => Revisions(userId).Where(r => r.FileId == fileId).OrderBy(r => r.Id).ToList()));

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredRevision>>(Read(() => Revisions(userId).Where(r => revisionIds.Contains(r.Id)).OrderBy(r => r.Id).ToList()));

    /// <inheritdoc/>
    public Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<long>>(Read(() => Revisions(userId)
            .Where(r => r.Operation == MemoryOperation.Undo)
            .Select(r => r.UndoesRevisionId)
            .OfType<long>()
            .Where(revisionIds.Contains)
            .ToHashSet()));

    /// <inheritdoc/>
    public Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (current is not null && (!_files.TryGetValue(current.Id, out var stored) || stored.UserId != userId || stored.File.Version != current.Version))
            {
                return Task.FromResult<SavedChange?>(null);
            }

            var id = current?.Id ?? Guid.NewGuid();
            if (!next.Deleted && Files(userId).Any(f => f.IsLive && f.Path == next.Path && f.Id != id))
            {
                return Task.FromResult<SavedChange?>(null);
            }

            var revisionId = _nextRevision++;
            _revisions.Add((userId, new StoredRevision(revisionId, id, next.Path, next.Content, next.Sensitivity, next.Deleted, operation, actor.Author,
                actor.ConversationId, actor.MessageId, undoesRevisionId, now)));
            _files[id] = (userId, new StoredFile(id, next.Path, next.Content, next.Sensitivity, next.SizeBytes, next.LineCount, next.Header, next.RowCount,
                (current?.Version ?? 0) + 1, revisionId, current?.CreatedAt ?? now, now, next.Deleted ? now : null));
            return Task.FromResult<SavedChange?>(new SavedChange(revisionId, id));
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    /// <inheritdoc/>
    public Task<MemoryEvent> AppendEventAsync(Guid userId, NewMemoryEvent next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var stored = MemoryEvents.From(_nextEvent++, next, now);
            _events.Add((userId, stored));
            return Task.FromResult(stored);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MemoryEvent>> AppendEventsAsync(Guid userId, IReadOnlyList<NewMemoryEvent> next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var stored = next.Select(e => MemoryEvents.From(_nextEvent++, e, now)).ToList();
            _events.AddRange(stored.Select(e => (userId, e)));
            return Task.FromResult<IReadOnlyList<MemoryEvent>>(stored);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MemoryEvent>>(Read(() => _events.Where(e => e.UserId == userId && query.Matches(e.Event)).Select(e => e.Event).ToList()));

    /// <inheritdoc/>
    public Task SaveVectorsAsync(Guid userId, IReadOnlyList<(long EventId, float[] Vector)> vectors, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var own = _events.Where(e => e.UserId == userId).Select(e => e.Event.Id).ToHashSet();
            foreach (var (id, vector) in vectors.Where(v => own.Contains(v.EventId)))
            {
                _vectors[(userId, id)] = vector;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<long, float[]>> VectorsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, float[]>>(Read(() => _vectors.Where(v => v.Key.UserId == userId).ToDictionary(v => v.Key.EventId, v => v.Value)));

    /// <inheritdoc/>
    public Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _events.RemoveAll(e => e.UserId == userId && eventIds.Contains(e.Event.Id));
            foreach (var id in eventIds)
            {
                _vectors.Remove((userId, id));
            }

            _revisions.RemoveAll(r => r.UserId == userId && fileIds.Contains(r.Revision.FileId));
            foreach (var id in fileIds.Where(id => _files.TryGetValue(id, out var f) && f.UserId == userId))
            {
                _files.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    private T Read<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private IEnumerable<StoredFile> Files(Guid userId) => _files.Values.Where(f => f.UserId == userId).Select(f => f.File);

    private IEnumerable<StoredRevision> Revisions(Guid userId) => _revisions.Where(r => r.UserId == userId).Select(r => r.Revision);

    private IEnumerable<StoredFile> Visible(Guid userId, string? prefix, bool includePrivate) =>
        Files(userId).Where(f => f.IsLive
            && (includePrivate || f.Sensitivity != MemorySensitivity.Private)
            && (string.IsNullOrEmpty(prefix) || prefix == "/" || f.Path.StartsWith(prefix, StringComparison.Ordinal)));
}
