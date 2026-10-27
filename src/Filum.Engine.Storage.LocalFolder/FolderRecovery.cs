using System.Text;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Storage.LocalFolder;

// A write appends its revision, then writes the file, then saves the state (spec 039). A crash after the revision leaves
// the log ahead of the files: before the disk is scanned for hand edits, those writes are completed as they were made,
// so they are never taken for the person's.
internal static class FolderRecovery
{
    public static void CompleteInterruptedWrites(FolderSession session)
    {
        if (session.StateRebuilt)
        {
            CompleteFromTheLog(session);
        }
        else
        {
            ApplyRevisionsAfterTheState(session);
        }
    }

    // The saved state stopped at its last revision: every later one is applied to the files and the state.
    private static void ApplyRevisionsAfterTheState(FolderSession session)
    {
        var last = session.State.LastRevision;
        if (session.Revisions.LastId() <= last)
        {
            return;
        }

        foreach (var revision in session.Revisions.All().Where(r => r.Id > last))
        {
            var entry = session.State.Files.FirstOrDefault(e => e.Id == revision.FileId);
            if (entry is null)
            {
                entry = new FileEntry { Id = revision.FileId, CreatedAt = revision.CreatedAt };
                session.State.Files.Add(entry);
            }

            if (entry.DeletedAt is null && (revision.Deleted || entry.Path != revision.Path))
            {
                session.Disk.Remove(entry.Path);
            }

            Complete(session, entry, revision);
            session.State.LastRevision = Math.Max(session.State.LastRevision, revision.Id);
        }

        session.MarkChanged();
    }

    // A state rebuilt from the log already holds every revision; a file still as its revision before the latest one
    // was written by a write that stopped, and is completed.
    private static void CompleteFromTheLog(FolderSession session)
    {
        foreach (var entry in session.State.Files.ToList())
        {
            var onDisk = session.Disk.Exists(entry.Path) ? session.Disk.Read(entry.Path) : null;
            var latest = entry.DeletedAt is null ? onDisk is not null && FileMeasures.Hash(onDisk) == entry.Hash : onDisk is null;
            if (latest)
            {
                continue;
            }

            var revisions = session.Revisions.OfFile(entry.Id);
            if (revisions.Count < 2)
            {
                continue;
            }

            var before = revisions[^2];
            var asBefore = before.Deleted ? onDisk is null : onDisk is not null && before.Path == entry.Path && onDisk == before.Content;
            if (asBefore)
            {
                Complete(session, entry, revisions[^1]);
                session.MarkChanged();
            }
        }
    }

    private static void Complete(FolderSession session, FileEntry entry, StoredRevision revision)
    {
        if (revision.Deleted)
        {
            session.Disk.Remove(revision.Path);
        }
        else
        {
            session.Disk.Write(revision.Path, revision.Content);
        }

        var (header, rows) = FileMeasures.CollectionShape(revision.Path, revision.Content);
        session.Apply(entry, revision, Encoding.UTF8.GetByteCount(revision.Content), MemoryText.LinesOf(revision.Content).Count, header, rows);
    }
}
