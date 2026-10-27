using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Storage.LocalFolder;

// What the person changed by hand since the last call becomes their revisions (operation Outside), in path order.
internal static class FolderScanner
{
    public static void Scan(FolderSession session, DateTimeOffset now)
    {
        var onDisk = session.Disk.Valid();
        foreach (var entry in session.State.Live().OrderBy(e => e.Path, StringComparer.Ordinal).ToList())
        {
            if (!onDisk.TryGetValue(entry.Path, out var info))
            {
                var last = session.Content(entry);
                Adopt(session, entry, entry.Path, last, deleted: true, now);
                continue;
            }

            // Same length and time: untouched, without reading it.
            if (info.Length == entry.DiskLength && info.LastWriteTimeUtc == entry.DiskTimeUtc)
            {
                continue;
            }

            var content = session.Disk.Read(entry.Path);
            if (FileMeasures.Hash(content) == entry.Hash)
            {
                entry.DiskLength = info.Length;
                entry.DiskTimeUtc = info.LastWriteTimeUtc;
                session.MarkChanged();
            }
            else
            {
                Adopt(session, entry, entry.Path, content, deleted: false, now);
            }
        }

        var known = session.State.Live().Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in onDisk.Keys.Where(p => !known.Contains(p)).Order(StringComparer.Ordinal))
        {
            var entry = new FileEntry { Id = Guid.NewGuid(), CreatedAt = now };
            session.State.Files.Add(entry);
            Adopt(session, entry, path, session.Disk.Read(path), deleted: false, now);
        }
    }

    private static void Adopt(FolderSession session, FileEntry entry, string path, string content, bool deleted, DateTimeOffset now)
    {
        var revision = session.AppendRevision(entry, path, content, entry.Sensitivity, deleted, MemoryOperation.Outside, MemoryAuthor.Person, null, null, null, now);
        var (header, rows) = FileMeasures.CollectionShape(path, content);
        session.Apply(entry, revision, Encoding.UTF8.GetByteCount(content), MemoryText.LinesOf(content).Count, header, rows);
    }
}
