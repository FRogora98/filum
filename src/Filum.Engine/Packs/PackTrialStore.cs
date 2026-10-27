using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Packs;

// It keeps a real store's rules for files (one live file per path, versions checked) so the trial refuses what a
// person's memory would.
internal sealed class PackTrialStore : IMemoryFileStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, (Guid UserId, StoredFile File)> _files = [];
    private long _nextRevision = 1;

    public Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).FirstOrDefault(f => f.IsLive && f.Path == path)));

    public Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).FirstOrDefault(f => f.Id == fileId)));

    public Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredFile>>(Read(() => Visible(userId, prefix, includePrivate).ToList()));

    public Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredFile>>(Read(() => Visible(userId, prefix, includePrivate).Where(f => f.Content.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList()));

    public Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).Count(f => f.IsLive)));

    public Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken) =>
        Task.FromResult(Read(() => Files(userId).Any(f => f.IsLive && f.Path == path && f.Id != exceptFileId)));

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
            _files[id] = (userId, new StoredFile(id, next.Path, next.Content, next.Sensitivity, next.SizeBytes, next.LineCount, next.Header, next.RowCount,
                (current?.Version ?? 0) + 1, revisionId, current?.CreatedAt ?? now, now, next.Deleted ? now : null));
            return Task.FromResult<SavedChange?>(new SavedChange(revisionId, id));
        }
    }

    private T Read<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private IEnumerable<StoredFile> Files(Guid userId) => _files.Values.Where(f => f.UserId == userId).Select(f => f.File);

    private IEnumerable<StoredFile> Visible(Guid userId, string? prefix, bool includePrivate) =>
        Files(userId).Where(f => f.IsLive
            && (includePrivate || f.Sensitivity != MemorySensitivity.Private)
            && (string.IsNullOrEmpty(prefix) || prefix == "/" || f.Path.StartsWith(prefix, StringComparison.Ordinal)));
}
