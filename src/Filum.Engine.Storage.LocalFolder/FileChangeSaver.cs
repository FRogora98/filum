using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder;

internal static class FileChangeSaver
{
    // Null when the file moved on since it was read, or another live file took the path.
    public static SavedChange? Save(FolderSession session, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now)
    {
        var entry = current is null ? null : session.State.Files.FirstOrDefault(e => e.Id == current.Id);
        if (current is not null && (entry is null || entry.Version != current.Version))
        {
            return null;
        }

        var id = current?.Id ?? Guid.NewGuid();
        if (!next.Deleted && session.State.Live().Any(e => e.Path == next.Path && e.Id != id))
        {
            return null;
        }

        var created = entry is null;
        entry ??= new FileEntry { Id = id, CreatedAt = now };
        var oldPath = created || entry.DeletedAt is not null ? null : entry.Path;

        // The revision first (spec 039): a crash after it leaves a change the next opening completes as the agent's,
        // never a file the log does not know, which would be adopted as the person's.
        var revision = session.AppendRevision(entry, next.Path, next.Content, next.Sensitivity, next.Deleted, operation, actor.Author, actor.ConversationId, actor.MessageId, undoesRevisionId, now,
            previousContent: current?.Content);
        if (created)
        {
            session.State.Files.Add(entry);
        }

        if (oldPath is not null && (next.Deleted || oldPath != next.Path))
        {
            session.Disk.Remove(oldPath);
        }

        if (!next.Deleted)
        {
            session.Disk.Write(next.Path, next.Content);
        }

        session.Apply(entry, revision, next.SizeBytes, next.LineCount, next.Header, next.RowCount);
        return new SavedChange(revision.Id, id);
    }
}
