using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder;

// Every id comes from here, so the log's last id wins over a state saved before a crash.
internal sealed class FolderSession
{
    private readonly StateFile _stateFile;
    private bool _dirty;

    public FolderSession(FolderLayout layout)
    {
        Disk = new DiskFiles(layout);
        Revisions = new RevisionLog(layout);
        Events = new EventLog(layout);
        Vectors = new VectorFile(layout);
        _stateFile = new StateFile(layout);
        State = LoadState();
    }

    public FolderState State { get; }

    // True when state.json was missing or damaged and the state was rebuilt from the log.
    public bool StateRebuilt { get; private set; }

    public DiskFiles Disk { get; }

    public RevisionLog Revisions { get; }

    public EventLog Events { get; }

    public VectorFile Vectors { get; }

    public void MarkChanged() => _dirty = true;

    public void SaveState()
    {
        if (_dirty)
        {
            _stateFile.Write(State);
        }
    }

    public StoredFile Stored(FileEntry e, bool withContent = true) =>
        new(e.Id, e.Path, withContent ? Content(e) : string.Empty, e.Sensitivity, e.SizeBytes, e.LineCount, e.Header, e.RowCount,
            e.Version, e.LatestRevisionId, e.CreatedAt, e.UpdatedAt, e.DeletedAt);

    // A live file's content comes from disk; a deleted one's from its latest revision.
    public string Content(FileEntry entry) =>
        entry.DeletedAt is null && Disk.Exists(entry.Path)
            ? Disk.Read(entry.Path)
            : Revisions.LatestContentOf(entry.Id);

    // previousContent: what the file held before this change, when known; the revision is written as a change from the
    // file's latest revision only when that content is the latest revision's (its hash is the entry's).
    public StoredRevision AppendRevision(FileEntry entry, string path, string content, string sensitivity, bool deleted, string operation, string author,
        Guid? conversationId, Guid? messageId, long? undoes, DateTimeOffset now, string? previousContent = null)
    {
        State.LastRevision = Math.Max(State.LastRevision, Revisions.LastId());
        var revision = new StoredRevision(++State.LastRevision, entry.Id, path, content, sensitivity, deleted, operation, author, conversationId, messageId, undoes, now);
        var previous = previousContent is not null && entry.LatestRevisionId > 0 && FileMeasures.Hash(previousContent) == entry.Hash
            ? (entry.LatestRevisionId, previousContent, entry.RevisionDepth)
            : ((long, string, int)?)null;
        entry.RevisionDepth = Revisions.Append(revision, previous);
        _dirty = true;
        return revision;
    }

    public void Apply(FileEntry entry, StoredRevision revision, int size, int lines, string? header, int? rows)
    {
        entry.Path = revision.Path;
        entry.Sensitivity = revision.Sensitivity;
        entry.SizeBytes = size;
        entry.LineCount = lines;
        entry.Header = header;
        entry.RowCount = rows;
        entry.Version++;
        entry.LatestRevisionId = revision.Id;
        entry.UpdatedAt = revision.CreatedAt;
        entry.DeletedAt = revision.Deleted ? revision.CreatedAt : null;
        entry.Hash = FileMeasures.Hash(revision.Content);
        var disk = revision.Deleted ? null : Disk.Info(revision.Path);
        entry.DiskLength = disk?.Length ?? 0;
        entry.DiskTimeUtc = disk?.LastWriteTimeUtc ?? default;
        _dirty = true;
    }

    public MemoryEvent AppendEvent(NewMemoryEvent next, DateTimeOffset now)
    {
        State.LastEvent = Math.Max(State.LastEvent, Events.LastId());
        var stored = MemoryEvents.From(++State.LastEvent, next, now);
        Events.Append(stored);
        _dirty = true;
        return stored;
    }

    // The vectors of events the log no longer has are dropped.
    public void SaveVectors(IReadOnlyList<(long EventId, float[] Vector)> vectors)
    {
        var known = Events.All().Select(e => e.Id).ToHashSet();
        Vectors.Save(vectors.Where(v => known.Contains(v.EventId)));
    }

    private FolderState LoadState()
    {
        if (_stateFile.Read() is { } saved)
        {
            return saved;
        }

        StateRebuilt = true;
        var state = StateFile.Rebuild(Revisions.All());
        foreach (var (fileId, depth) in Revisions.Depths())
        {
            if (state.Files.FirstOrDefault(e => e.Id == fileId) is { } entry)
            {
                entry.RevisionDepth = depth;
            }
        }

        _dirty = true;
        return state;
    }
}
