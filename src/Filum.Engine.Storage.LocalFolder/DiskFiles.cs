using System.Text;
using Filum.Engine.Memory;

namespace Filum.Engine.Storage.LocalFolder;

internal sealed class DiskFiles(FolderLayout layout)
{
    public bool Exists(string path) => File.Exists(layout.Full(path));

    public FileInfo Info(string path) => new(layout.Full(path));

    public string Read(string path) => File.ReadAllText(layout.Full(path), Encoding.UTF8).Replace("\r\n", "\n");

    public void Write(string path, string content)
    {
        var full = layout.Full(path);
        if (Path.GetDirectoryName(full) is { } folder)
        {
            Directory.CreateDirectory(folder);
        }

        AtomicFile.Write(layout, full, content);
    }

    public void Remove(string path)
    {
        var full = layout.Full(path);
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        // Folders left empty go too, up to the memory's root.
        for (var dir = Path.GetDirectoryName(full); dir is not null && dir.Length > layout.Root.Length && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any(); dir = Path.GetDirectoryName(dir))
        {
            Directory.Delete(dir);
        }
    }

    // Hidden names and .filum/ are not memory.
    public Dictionary<string, FileInfo> Valid()
    {
        var files = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        foreach (var full in Directory.EnumerateFiles(layout.Root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(layout.Root, full).Replace(Path.DirectorySeparatorChar, '/');
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
}
