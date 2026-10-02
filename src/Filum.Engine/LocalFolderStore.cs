using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Filum.Engine;

/// <summary>
/// A person's memory in a folder on their own disk (spec 012): every file is a real file at its memory path
/// (<c>/notes/plans.md</c> is <c>notes/plans.md</c>), readable and editable with any editor. Beside them,
/// <c>.filum/</c> keeps an append-only log of every revision (<c>revisions.jsonl</c>), the log of events
/// (<c>events.jsonl</c>, spec 030), the current state
/// (<c>state.json</c>, rebuilt from the log when missing) and the owner. Changes the person makes by hand are adopted
/// as their revisions before every call. A lock file serializes the processes that share the folder. One person per
/// folder: for anyone else the memory is empty and a change is refused.
/// </summary>
public sealed class LocalFolderStore : IMemoryStore
{
    private const string MetaFolder = ".filum";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    private readonly string _folder;
    private readonly string _meta;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalFolderStore(string folder, Guid? owner = null)
    {
        _folder = Path.GetFullPath(folder);
        _meta = Path.Combine(_folder, MetaFolder);
        Directory.CreateDirectory(Path.Combine(_meta, "tmp"));
        var ownerPath = Path.Combine(_meta, "owner");
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
    public static string DefaultFolder() =>
        Environment.GetEnvironmentVariable("FILUM_HOME") is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".filum");

    public Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.Live().FirstOrDefault(e => e.Path == path) is { } e ? s.Stored(e) : null, cancellationToken);

    public Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.State.Files.FirstOrDefault(e => e.Id == fileId) is { } e ? s.Stored(e) : null, cancellationToken);

    public Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredFile>>(userId, [], s => s.Visible(prefix, includePrivate).Select(e => s.Stored(e, withContent)).ToList(), cancellationToken);

    public Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredFile>>(userId, [], s => s.Visible(prefix, includePrivate)
            .Select(e => s.Stored(e))
            .Where(f => f.Content.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList(), cancellationToken);

    public Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Read(userId, 0, s => s.Live().Count(), cancellationToken);

    public Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken) =>
        Read(userId, false, s => s.Live().Any(e => e.Path == path && e.Id != exceptFileId), cancellationToken);

    public Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken) =>
        Read(userId, null, s => s.Log().FirstOrDefault(r => r.Id == revisionId), cancellationToken);

    public Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredRevision>>(userId, [], s => s.Log().Where(r => r.FileId == fileId).ToList(), cancellationToken);

    public Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<StoredRevision>>(userId, [], s => s.Log().Where(r => revisionIds.Contains(r.Id)).ToList(), cancellationToken);

    public Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken) =>
        Read<IReadOnlySet<long>>(userId, new HashSet<long>(), s => s.Log()
            .Where(r => r.Operation == MemoryOperation.Undo && r.UndoesRevisionId is { } undone && revisionIds.Contains(undone))
            .Select(r => r.UndoesRevisionId!.Value)
            .ToHashSet(), cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    public async Task<MemoryEvent> AppendEventAsync(Guid userId, NewMemoryEvent next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (userId != Owner)
        {
            throw new UnauthorizedAccessException("This memory belongs to another person.");
        }

        return await Locked(s => s.AppendEvent(next, now), cancellationToken);
    }

    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        Read<IReadOnlyList<MemoryEvent>>(userId, [], s => s.Events().Where(query.Matches).ToList(), cancellationToken);

    public async Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        if (userId != Owner)
        {
            return;
        }

        await Locked(s =>
        {
            s.Forget(eventIds, fileIds);
            return true;
        }, cancellationToken);
    }

    public async Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (userId != Owner)
        {
            throw new UnauthorizedAccessException("This memory belongs to another person.");
        }

        return await Locked(s =>
        {
            var entry = current is null ? null : s.State.Files.FirstOrDefault(e => e.Id == current.Id);
            if (current is not null && (entry is null || entry.Version != current.Version))
            {
                return null;
            }

            var id = current?.Id ?? Guid.NewGuid();
            if (!next.Deleted && s.Live().Any(e => e.Path == next.Path && e.Id != id))
            {
                return null;
            }

            if (entry is { DeletedAt: null } && (next.Deleted || entry.Path != next.Path))
            {
                s.RemoveFromDisk(entry.Path);
            }

            if (!next.Deleted)
            {
                s.WriteToDisk(next.Path, next.Content);
            }

            if (entry is null)
            {
                entry = new Entry { Id = id, CreatedAt = now };
                s.State.Files.Add(entry);
            }

            var revision = s.Append(id, next.Path, next.Content, next.Sensitivity, next.Deleted, operation, actor.Author, actor.ConversationId, actor.MessageId, undoesRevisionId, now);
            s.Apply(entry, revision, next.SizeBytes, next.LineCount, next.Header, next.RowCount);
            return new SavedChange(revision.Id, id);
        }, cancellationToken);
    }

    private Task<T> Read<T>(Guid userId, T nobody, Func<Session, T> read, CancellationToken cancellationToken) =>
        userId != Owner ? Task.FromResult(nobody) : Locked(read, cancellationToken);

    /// <summary>One call: the folder is locked, the state loaded, the disk scanned for hand edits, then the action runs.</summary>
    private async Task<T> Locked<T>(Func<Session, T> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var folderLock = await AcquireLockAsync(cancellationToken);
            var session = new Session(this);
            session.Scan();
            var result = action(session);
            session.SaveState();
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_meta, "lock");
        var until = DateTimeOffset.UtcNow + LockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    private sealed class State
    {
        public long LastRevision { get; set; }

        public long LastEvent { get; set; }

        public List<Entry> Files { get; set; } = [];
    }

    /// <summary>A file as the state keeps it; the disk fields tell a hand edit from an untouched file.</summary>
    private sealed class Entry
    {
        public Guid Id { get; set; }

        public string Path { get; set; } = string.Empty;

        public string Sensitivity { get; set; } = MemorySensitivity.Normal;

        public int SizeBytes { get; set; }

        public int LineCount { get; set; }

        public string? Header { get; set; }

        public int? RowCount { get; set; }

        public int Version { get; set; }

        public long LatestRevisionId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public string Hash { get; set; } = string.Empty;

        public long DiskLength { get; set; }

        public DateTime DiskTimeUtc { get; set; }
    }

    /// <summary>The work of one locked call: the state as read, the log, and what changed.</summary>
    private sealed class Session
    {
        private readonly LocalFolderStore _store;
        private List<StoredRevision>? _log;
        private List<MemoryEvent>? _events;
        private bool _dirty;

        public Session(LocalFolderStore store)
        {
            _store = store;
            State = LoadState();
        }

        public State State { get; }

        private string StatePath => Path.Combine(_store._meta, "state.json");

        private string LogPath => Path.Combine(_store._meta, "revisions.jsonl");

        private string EventsPath => Path.Combine(_store._meta, "events.jsonl");

        /// <summary>The events, oldest first; a line that does not parse (a write cut short) is skipped.</summary>
        public List<MemoryEvent> Events() => _events ??= ReadLines<MemoryEvent>(EventsPath);

        public MemoryEvent AppendEvent(NewMemoryEvent next, DateTimeOffset now)
        {
            // The state can be rebuilt without its counter: the log's last id still wins.
            State.LastEvent = Math.Max(State.LastEvent, Events().Select(e => e.Id).DefaultIfEmpty(0).Max());
            var stored = MemoryEvents.From(++State.LastEvent, next, now);
            AppendLine(EventsPath, JsonSerializer.Serialize(stored, Json));
            Events().Add(stored);
            _dirty = true;
            return stored;
        }

        /// <summary>Removes events and whole files (their revisions and what is on disk) for good.</summary>
        public void Forget(IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds)
        {
            if (eventIds.Count > 0)
            {
                Events().RemoveAll(e => eventIds.Contains(e.Id));
                Rewrite(EventsPath, Events().Select(e => JsonSerializer.Serialize(e, Json)));
            }

            if (fileIds.Count > 0)
            {
                foreach (var entry in State.Files.Where(e => fileIds.Contains(e.Id)).ToList())
                {
                    if (entry.DeletedAt is null)
                    {
                        RemoveFromDisk(entry.Path);
                    }

                    State.Files.Remove(entry);
                }

                Log().RemoveAll(r => fileIds.Contains(r.FileId));
                Rewrite(LogPath, Log().Select(r => JsonSerializer.Serialize(r, Json)));
            }

            _dirty = true;
        }

        private void Rewrite(string path, IEnumerable<string> lines)
        {
            var temporary = System.IO.Path.Combine(_store._meta, "tmp", Guid.NewGuid().ToString("N"));
            File.WriteAllText(temporary, string.Concat(lines.Select(l => l + "\n")), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }

        private static List<T> ReadLines<T>(string path)
        {
            var items = new List<T>();
            if (!File.Exists(path))
            {
                return items;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<T>(line, Json) is { } item)
                    {
                        items.Add(item);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return items;
        }

        /// <summary>Appends one line and flushes it; a line cut short by a crash is closed first.</summary>
        private static void AppendLine(string path, string line)
        {
            using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            if (file.Length > 0 && LastByte(path) != (byte)'\n')
            {
                file.WriteByte((byte)'\n');
            }

            file.Write(Encoding.UTF8.GetBytes(line + "\n"));
            file.Flush(flushToDisk: true);
        }

        public IEnumerable<Entry> Live() => State.Files.Where(e => e.DeletedAt is null);

        public IEnumerable<Entry> Visible(string? prefix, bool includePrivate) =>
            Live().Where(e => (includePrivate || e.Sensitivity != MemorySensitivity.Private)
                && (string.IsNullOrEmpty(prefix) || prefix == "/" || e.Path.StartsWith(prefix, StringComparison.Ordinal)));

        public StoredFile Stored(Entry e, bool withContent = true) =>
            new(e.Id, e.Path, withContent ? Content(e) : string.Empty, e.Sensitivity, e.SizeBytes, e.LineCount, e.Header, e.RowCount,
                e.Version, e.LatestRevisionId, e.CreatedAt, e.UpdatedAt, e.DeletedAt);

        /// <summary>The revisions, oldest first; a line that does not parse (a write cut short) is skipped.</summary>
        public List<StoredRevision> Log()
        {
            if (_log is not null)
            {
                return _log;
            }

            _log = ReadLines<StoredRevision>(LogPath);
            return _log;
        }

        public StoredRevision Append(Guid fileId, string path, string content, string sensitivity, bool deleted, string operation, string author,
            Guid? conversationId, Guid? messageId, long? undoes, DateTimeOffset now)
        {
            var revision = new StoredRevision(++State.LastRevision, fileId, path, content, sensitivity, deleted, operation, author, conversationId, messageId, undoes, now);
            AppendLine(LogPath, JsonSerializer.Serialize(revision, Json));

            Log().Add(revision);
            _dirty = true;
            return revision;
        }

        /// <summary>The state entry after a revision: its new path, level, sizes, version and the file on disk.</summary>
        public void Apply(Entry entry, StoredRevision revision, int size, int lines, string? header, int? rows)
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
            entry.Hash = Hash(revision.Content);
            var disk = revision.Deleted ? null : new FileInfo(Full(revision.Path));
            entry.DiskLength = disk?.Length ?? 0;
            entry.DiskTimeUtc = disk?.LastWriteTimeUtc ?? default;
            _dirty = true;
        }

        public void WriteToDisk(string path, string content)
        {
            var full = Full(path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            var temporary = System.IO.Path.Combine(_store._meta, "tmp", Guid.NewGuid().ToString("N"));
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, full, overwrite: true);
        }

        public void RemoveFromDisk(string path)
        {
            var full = Full(path);
            if (File.Exists(full))
            {
                File.Delete(full);
            }

            // Folders left empty go too, up to the memory's root.
            for (var dir = System.IO.Path.GetDirectoryName(full); dir is not null && dir.Length > _store._folder.Length && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any(); dir = System.IO.Path.GetDirectoryName(dir))
            {
                Directory.Delete(dir);
            }
        }

        /// <summary>
        /// Adopts what the person changed by hand since the last call, as their revisions (operation
        /// <see cref="MemoryOperation.Outside"/>): edited files, new valid files, removed files, in path order.
        /// </summary>
        public void Scan()
        {
            var onDisk = DiskFiles();
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in Live().OrderBy(e => e.Path, StringComparer.Ordinal).ToList())
            {
                if (!onDisk.TryGetValue(entry.Path, out var info))
                {
                    var last = Content(entry);
                    Adopt(entry, entry.Path, last, deleted: true, now);
                    continue;
                }

                if (info.Length == entry.DiskLength && info.LastWriteTimeUtc == entry.DiskTimeUtc)
                {
                    continue;
                }

                var content = ReadDisk(entry.Path);
                if (Hash(content) == entry.Hash)
                {
                    entry.DiskLength = info.Length;
                    entry.DiskTimeUtc = info.LastWriteTimeUtc;
                    _dirty = true;
                }
                else
                {
                    Adopt(entry, entry.Path, content, deleted: false, now);
                }
            }

            var known = Live().Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
            foreach (var path in onDisk.Keys.Where(p => !known.Contains(p)).Order(StringComparer.Ordinal))
            {
                var entry = new Entry { Id = Guid.NewGuid(), CreatedAt = now };
                State.Files.Add(entry);
                Adopt(entry, path, ReadDisk(path), deleted: false, now);
            }
        }

        private void Adopt(Entry entry, string path, string content, bool deleted, DateTimeOffset now)
        {
            var revision = Append(entry.Id, path, content, entry.Sensitivity, deleted, MemoryOperation.Outside, MemoryAuthor.Person, null, null, null, now);
            var (header, rows) = CollectionShape(path, content);
            Apply(entry, revision, Encoding.UTF8.GetByteCount(content), MemoryService.LinesOf(content).Count, header, rows);
        }

        public void SaveState()
        {
            if (!_dirty)
            {
                return;
            }

            var temporary = System.IO.Path.Combine(_store._meta, "tmp", Guid.NewGuid().ToString("N"));
            File.WriteAllText(temporary, JsonSerializer.Serialize(State, Json), new UTF8Encoding(false));
            File.Move(temporary, StatePath, overwrite: true);
        }

        /// <summary>The state as saved, or rebuilt from the log when it is missing or damaged.</summary>
        private State LoadState()
        {
            if (File.Exists(StatePath))
            {
                try
                {
                    if (JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath), Json) is { } saved)
                    {
                        return saved;
                    }
                }
                catch (JsonException)
                {
                }
            }

            var state = new State();
            foreach (var revision in Log())
            {
                var entry = state.Files.FirstOrDefault(e => e.Id == revision.FileId);
                if (entry is null)
                {
                    entry = new Entry { Id = revision.FileId, CreatedAt = revision.CreatedAt };
                    state.Files.Add(entry);
                }

                var (header, rows) = CollectionShape(revision.Path, revision.Content);
                entry.Path = revision.Path;
                entry.Sensitivity = revision.Sensitivity;
                entry.SizeBytes = Encoding.UTF8.GetByteCount(revision.Content);
                entry.LineCount = MemoryService.LinesOf(revision.Content).Count;
                entry.Header = header;
                entry.RowCount = rows;
                entry.Version++;
                entry.LatestRevisionId = revision.Id;
                entry.UpdatedAt = revision.CreatedAt;
                entry.DeletedAt = revision.Deleted ? revision.CreatedAt : null;
                entry.Hash = Hash(revision.Content);
                state.LastRevision = Math.Max(state.LastRevision, revision.Id);
            }

            _dirty = true;
            return state;
        }

        /// <summary>A live file's content comes from disk; a deleted one's from its latest revision.</summary>
        private string Content(Entry entry) =>
            entry.DeletedAt is null && File.Exists(Full(entry.Path))
                ? ReadDisk(entry.Path)
                : Log().LastOrDefault(r => r.FileId == entry.Id)?.Content ?? string.Empty;

        private string ReadDisk(string path) => File.ReadAllText(Full(path), Encoding.UTF8).Replace("\r\n", "\n");

        private string Full(string path) => System.IO.Path.Combine(_store._folder, path.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));

        /// <summary>The valid memory files on disk, by memory path; hidden names and <c>.filum/</c> are not memory.</summary>
        private Dictionary<string, FileInfo> DiskFiles()
        {
            var files = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
            foreach (var full in Directory.EnumerateFiles(_store._folder, "*", SearchOption.AllDirectories))
            {
                var relative = System.IO.Path.GetRelativePath(_store._folder, full).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                if (relative.Split('/').Any(segment => segment.StartsWith('.')))
                {
                    continue;
                }

                var path = "/" + relative;
                if (MemoryPaths.Validate(path) is null)
                {
                    files[path] = new FileInfo(full);
                }
            }

            return files;
        }

        private static byte LastByte(string path)
        {
            using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            read.Seek(-1, SeekOrigin.End);
            return (byte)read.ReadByte();
        }

        private static (string? Header, int? Rows) CollectionShape(string path, string content)
        {
            if (MemoryPaths.KindOf(path) != MemoryFileKind.Collection)
            {
                return (null, null);
            }

            var table = Csv.Check(content);
            return table.IsValid ? (string.Join(",", table.Header), table.Rows.Count) : (null, null);
        }

        private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
