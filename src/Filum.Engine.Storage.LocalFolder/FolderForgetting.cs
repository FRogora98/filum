namespace Filum.Engine.Storage.LocalFolder;

// For good: events with their vectors, files with their revisions and what is on disk.
internal static class FolderForgetting
{
    public static void Forget(FolderSession session, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds)
    {
        if (eventIds.Count > 0)
        {
            session.Events.Remove(eventIds);
            session.Vectors.Remove(eventIds);
        }

        if (fileIds.Count > 0)
        {
            foreach (var entry in session.State.Files.Where(e => fileIds.Contains(e.Id)).ToList())
            {
                if (entry.DeletedAt is null)
                {
                    session.Disk.Remove(entry.Path);
                }

                session.State.Files.Remove(entry);
            }

            session.Revisions.RemoveFiles(fileIds);
        }

        session.MarkChanged();
    }
}
