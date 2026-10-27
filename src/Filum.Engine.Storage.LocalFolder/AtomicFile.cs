using System.Text;

namespace Filum.Engine.Storage.LocalFolder;

// A crash leaves the old file or the new one, never half.
internal static class AtomicFile
{
    public static void Write(FolderLayout layout, string path, string content)
    {
        var temporary = layout.NewTemporaryFile();
        // On disk before the rename (spec 039), so a crash cannot leave the real name on an empty or partial file.
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(new UTF8Encoding(false).GetBytes(content));
            file.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
