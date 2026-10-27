using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Storage.LocalFolder;

// The properties and their order are the on-disk format.
internal sealed class FolderState
{
    public long LastRevision { get; set; }

    public long LastEvent { get; set; }

    public List<FileEntry> Files { get; set; } = [];

    public IEnumerable<FileEntry> Live() => Files.Where(e => e.DeletedAt is null);

    public IEnumerable<FileEntry> Visible(string? prefix, bool includePrivate) =>
        Live().Where(e => (includePrivate || e.Sensitivity != MemorySensitivity.Private)
            && (string.IsNullOrEmpty(prefix) || prefix == "/" || e.Path.StartsWith(prefix, StringComparison.Ordinal)));
}
