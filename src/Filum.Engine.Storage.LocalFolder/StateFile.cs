using System.Text.Json;
using System.Text;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Storage.LocalFolder;

// Saved after a call that changed it; rebuilt from the log when lost.
internal sealed class StateFile(FolderLayout layout)
{
    // Null when missing or damaged.
    public FolderState? Read()
    {
        if (!File.Exists(layout.StateFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FolderState>(File.ReadAllText(layout.StateFile), JsonLines.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(FolderState state) => AtomicFile.Write(layout, layout.StateFile, JsonLines.Serialize(state));

    // The disk fields stay empty, so the next scan reads every file once.
    public static FolderState Rebuild(IEnumerable<StoredRevision> revisions)
    {
        var state = new FolderState();
        foreach (var revision in revisions)
        {
            var entry = state.Files.FirstOrDefault(e => e.Id == revision.FileId);
            if (entry is null)
            {
                entry = new FileEntry { Id = revision.FileId, CreatedAt = revision.CreatedAt };
                state.Files.Add(entry);
            }

            var (header, rows) = FileMeasures.CollectionShape(revision.Path, revision.Content);
            entry.Path = revision.Path;
            entry.Sensitivity = revision.Sensitivity;
            entry.SizeBytes = Encoding.UTF8.GetByteCount(revision.Content);
            entry.LineCount = MemoryText.LinesOf(revision.Content).Count;
            entry.Header = header;
            entry.RowCount = rows;
            entry.Version++;
            entry.LatestRevisionId = revision.Id;
            entry.UpdatedAt = revision.CreatedAt;
            entry.DeletedAt = revision.Deleted ? revision.CreatedAt : null;
            entry.Hash = FileMeasures.Hash(revision.Content);
            state.LastRevision = Math.Max(state.LastRevision, revision.Id);
        }

        return state;
    }
}
