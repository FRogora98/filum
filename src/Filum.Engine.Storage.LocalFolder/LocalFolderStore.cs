using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
namespace Filum.Engine.Storage.LocalFolder;

/// <summary>
/// A person's memory in a folder on their own disk: every file is a real file at its memory path
/// (<c>/notes/plans.md</c> is <c>notes/plans.md</c>), readable and editable with any editor. Beside them,
/// <c>.filum/</c> keeps an append-only log of every revision (<c>revisions.jsonl</c>), the log of events
/// (<c>events.jsonl</c>), the current state (<c>state.json</c>, rebuilt from the log when missing) and the owner.
/// Changes the person makes by hand are adopted as their revisions before every call. A lock file serializes the
/// processes that share the folder. One person per folder: for anyone else the memory is empty and a change is refused.
/// </summary>
public sealed class LocalFolderStore : IMemoryStore, IMemoryVectorModels, IDisposable
{
    private const string HomeVariable = "FILUM_HOME";
    private const string HomeFolderName = ".filum";
    private const string AnotherPerson = "This memory belongs to another person.";

    private readonly FolderLayout _layout;
    private readonly FolderLock _lock;
    private readonly TimeProvider _time;

    /// <summary>
    /// The memory in <paramref name="folder"/>, created when missing, for its owner: the one already recorded there, or
    /// <paramref name="owner"/> (a new id when null) for a new folder. <paramref name="time"/> dates hand edits; the
    /// system clock when null.
    /// </summary>
    public LocalFolderStore(string folder, Guid? owner = null, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _layout = new FolderLayout(folder);
        FolderFormat.Check(_layout);
        Directory.CreateDirectory(_layout.Temporary);
        _lock = new FolderLock(_layout.LockFile, _time);
        var ownerPath = _layout.OwnerFile;
        if (File.Exists(ownerPath) && Guid.TryParse(File.ReadAllText(ownerPath).Trim(), out var existing))
        {
            Owner = existing;
        }
        else
        {
            Owner = owner ?? Guid.NewGuid();
            File.WriteAllText(ownerPath, Owner.ToString());
        }
    }

    /// <summary>The person this folder belongs to.</summary>
    public Guid Owner { get; }

    /// <summary>Where a memory lives by default: <c>FILUM_HOME</c> when set, otherwise <c>~/.filum</c>.</summary>
    [Obsolete("Removed in the next major version: the host decides the folder and passes it to the store.")]
    public static string DefaultFolder() =>
        Environment.GetEnvironmentVariable(HomeVariable) is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), HomeFolderName);

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    /// <inheritdoc/>
    public Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.State.Live().FirstOrDefault(e => e.Path == path) is { } e ? s.Stored(e) : null, cancellationToken);

    /// <inheritdoc/>
    public Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.State.Files.FirstOrDefault(e => e.Id == fileId) is { } e ? s.Stored(e) : null, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredFile>>(userId, [], s => s.State.Visible(prefix, includePrivate).Select(e => s.Stored(e, withContent)).ToList(), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredFile>>(userId, [], s => s.State.Visible(prefix, includePrivate)
            .Select(e => s.Stored(e))
            .Where(f => f.Content.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList(), cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Read(userId, 0, s => s.State.Live().Count(), cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken) =>
        Read(userId, false, s => s.State.Live().Any(e => e.Path == path && e.Id != exceptFileId), cancellationToken);

    /// <inheritdoc/>
    public Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.Revisions.Get(revisionId), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredRevision>>(userId, [], s => s.Revisions.OfFile(fileId), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredRevision>>(userId, [], s => s.Revisions.WithIds(revisionIds), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Read(userId, new HashSet<long>(), s => s.Revisions.UndoneAmong(revisionIds), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    /// <inheritdoc/>
    public async Task<MemoryEvent> AppendEventAsync(Guid userId, NewMemoryEvent next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EnsureOwner(userId);
        return await Locked(s => s.AppendEvent(next, now), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryEvent>> AppendEventsAsync(Guid userId, IReadOnlyList<NewMemoryEvent> next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EnsureOwner(userId);
        return await Locked<IReadOnlyList<MemoryEvent>>(s => next.Select(e => s.AppendEvent(e, now)).ToList(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<MemoryEvent>>(userId, [], s => s.Events.All().Where(query.Matches).ToList(), cancellationToken);

    /// <inheritdoc/>
    public async Task SaveVectorsAsync(Guid userId, IReadOnlyList<(long EventId, float[] Vector)> vectors, CancellationToken cancellationToken)
    {
        if (userId != Owner || vectors.Count == 0)
        {
            return;
        }

        await Locked(s =>
        {
            s.SaveVectors(vectors);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string?> VectorModelAsync(Guid userId, CancellationToken cancellationToken) =>
        Read<string?>(userId, null, _ => File.Exists(_layout.VectorModelFile) ? File.ReadAllText(_layout.VectorModelFile).Trim() : null, cancellationToken);

    /// <inheritdoc/>
    public async Task SetVectorModelAsync(Guid userId, string modelId, bool dropVectors, CancellationToken cancellationToken)
    {
        if (userId != Owner)
        {
            return;
        }

        await Locked(s =>
        {
            if (dropVectors)
            {
                s.Vectors.Clear();
            }

            AtomicFile.Write(_layout, _layout.VectorModelFile, modelId + "\n");
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<long, float[]>> VectorsAsync(Guid userId, CancellationToken cancellationToken) =>
        Read<IReadOnlyDictionary<long, float[]>>(userId, new Dictionary<long, float[]>(), s => s.Vectors.All(), cancellationToken);

    /// <inheritdoc/>
    public async Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        if (userId != Owner)
        {
            return;
        }

        await Locked(s =>
        {
            FolderForgetting.Forget(s, eventIds, fileIds);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EnsureOwner(userId);
        return await Locked(s => FileChangeSaver.Save(s, current, next, actor, operation, undoesRevisionId, now), cancellationToken);
    }

    private void EnsureOwner(Guid userId)
    {
        if (userId != Owner)
        {
            throw new UnauthorizedAccessException(AnotherPerson);
        }
    }

    private Task<T> Read<T>(Guid userId, T nobody, Func<FolderSession, T> read, CancellationToken cancellationToken) =>
        userId != Owner ? Task.FromResult(nobody) : Locked(read, cancellationToken);

    // The folder locked, the state loaded and the disk scanned for hand edits, then the action.
    private Task<T> Locked<T>(Func<FolderSession, T> action, CancellationToken cancellationToken) =>
        _lock.RunAsync(() =>
        {
            var session = new FolderSession(_layout);
            FolderRecovery.CompleteInterruptedWrites(session);
            FolderScanner.Scan(session, _time.GetUtcNow());
            var result = action(session);
            session.SaveState();
            return result;
        }, cancellationToken);
}
