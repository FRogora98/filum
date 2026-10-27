using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder;

// Appended in order; rewritten only to forget a file. A revision is written as its change from the previous revision
// of the same file, and whole again every MaxChain changes, so the log grows with what changed, not with the size of
// the file at each change. A write reads none of it: the last id comes from the end of the file. A read parses the
// lines once per call and rebuilds only the contents it returns.
internal sealed class RevisionLog(FolderLayout layout)
{
    // A version is never more than this many changes away from a whole content.
    public const int MaxChain = 256;

    // Enough to hold the last line's start; a longer line is read whole.
    private const int TailBytes = 64 * 1024;

    private List<RevisionLine>? _lines;
    private Dictionary<long, RevisionLine>? _byId;
    private readonly Dictionary<long, string> _contents = [];

    public long LastId()
    {
        if (_lines is not null)
        {
            return _lines.Select(l => l.Id).DefaultIfEmpty(0).Max();
        }

        return LastLine() is { } line ? line.Id : 0;
    }

    public StoredRevision? Get(long id) => ById().TryGetValue(id, out var line) ? Revision(line) : null;

    public IReadOnlyList<StoredRevision> OfFile(Guid fileId) => Lines().Where(l => l.FileId == fileId).Select(Revision).ToList();

    public IReadOnlyList<StoredRevision> WithIds(IReadOnlyCollection<long> ids) => Lines().Where(l => ids.Contains(l.Id)).Select(Revision).ToList();

    public IReadOnlySet<long> UndoneAmong(IReadOnlyCollection<long> ids) =>
        Lines().Where(l => l.Operation == MemoryOperation.Undo).Select(l => l.UndoesRevisionId).OfType<long>().Where(ids.Contains).ToHashSet();

    public string LatestContentOf(Guid fileId) => Lines().LastOrDefault(l => l.FileId == fileId) is { } line ? ContentOf(line) : string.Empty;

    // Every revision in order, for rebuilding the state; contents rebuilt one at a time.
    public IEnumerable<StoredRevision> All() => Lines().Select(Revision);

    // How many changes each file's latest revision is from a whole content.
    public Dictionary<Guid, int> Depths()
    {
        var depths = new Dictionary<Guid, int>();
        foreach (var line in Lines())
        {
            depths[line.FileId] = line.IsChange ? depths.GetValueOrDefault(line.FileId) + 1 : 0;
        }

        return depths;
    }

    // previous: the latest revision of the same file, its content and its depth; the revision is written whole without one
    // or when the chain is long. Returns the new revision's depth.
    public int Append(StoredRevision revision, (long Id, string Content, int Depth)? previous)
    {
        var line = previous is { } p && p.Depth < MaxChain ? RevisionLine.Change(revision, p.Id, p.Content) : RevisionLine.From(revision);
        JsonLines.Append(layout.RevisionsFile, JsonLines.Serialize(line));
        _lines?.Add(line);
        _byId?.TryAdd(line.Id, line);
        _contents[line.Id] = revision.Content;
        return line.IsChange ? previous!.Value.Depth + 1 : 0;
    }

    public void RemoveFiles(IReadOnlyCollection<Guid> fileIds)
    {
        Lines().RemoveAll(l => fileIds.Contains(l.FileId));
        _byId = null;
        JsonLines.Rewrite<RevisionLine>(layout, layout.RevisionsFile, Lines().Select(l => JsonLines.Serialize(l)));
    }

    private List<RevisionLine> Lines() => _lines ??= JsonLines.Read<RevisionLine>(layout.RevisionsFile);

    private Dictionary<long, RevisionLine> ById()
    {
        if (_byId is null)
        {
            _byId = [];
            foreach (var line in Lines())
            {
                _byId.TryAdd(line.Id, line);
            }
        }

        return _byId;
    }

    private StoredRevision Revision(RevisionLine line) => line.ToRevision(ContentOf(line));

    // Back to the last whole content of the chain, then forward through the changes, each checked against its hash. A
    // missing base or a content that does not match stops the read (spec 039): a wrong history is never returned.
    private string ContentOf(RevisionLine line)
    {
        var chain = new Stack<RevisionLine>();
        var current = line;
        string content;
        while (true)
        {
            if (_contents.TryGetValue(current.Id, out var known))
            {
                content = known;
                break;
            }

            if (!current.IsChange)
            {
                content = Checked(current, current.Content, line);
                break;
            }

            chain.Push(current);
            if (!ById().TryGetValue(current.Base!.Value, out var previous))
            {
                throw Damaged(line, current, $"the revision it changes, {current.Base}, is missing from the log");
            }

            current = previous;
        }

        while (chain.TryPop(out var change))
        {
            content = Checked(change, change.ContentAfter(content), line);
        }

        return content;
    }

    private string Checked(RevisionLine at, string content, RevisionLine asked)
    {
        if (!at.Holds(content))
        {
            throw Damaged(asked, at, "its content does not match the hash it was written with");
        }

        _contents[at.Id] = content;
        return content;
    }

    private static InvalidDataException Damaged(RevisionLine asked, RevisionLine at, string why) =>
        new($"The history of {asked.Path} is damaged: revision {asked.Id} cannot be rebuilt, because at revision {at.Id} {why}. " +
            "The files on disk are not affected; revisions of this file from that point on cannot be read.");

    // The last complete line of the file, read from its end.
    private RevisionLine? LastLine()
    {
        if (!File.Exists(layout.RevisionsFile))
        {
            return null;
        }

        using var file = new FileStream(layout.RevisionsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        for (var size = Math.Min(TailBytes, file.Length); ; size = Math.Min(size * 2, file.Length))
        {
            file.Seek(-size, SeekOrigin.End);
            var bytes = new byte[size];
            file.ReadExactly(bytes);
            var lines = Encoding.UTF8.GetString(bytes).Split('\n');
            // The first piece may be cut, unless the whole file was read.
            var whole = size == file.Length ? lines : lines[1..];
            if (JsonLines.Parse<RevisionLine>(whole).LastOrDefault() is { } last)
            {
                return last;
            }

            if (size == file.Length)
            {
                return null;
            }
        }
    }
}
