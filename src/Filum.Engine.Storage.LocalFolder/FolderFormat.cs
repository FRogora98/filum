using System.Globalization;
using System.Text;

namespace Filum.Engine.Storage.LocalFolder;

// The format of the folder's .filum files, kept in .filum/format. 1: the 0.7 format, every revision whole; a folder
// written before 1.0.0 (a pre-release) has no format file and is 1. 2: a revision can be a change from the previous one. 3 (spec 039), the one
// 1.0.0 writes:
// every revision line carries the hash of its content, and a write appends its revision before the file. An engine
// refuses a folder in a newer format than it knows instead of reading what it cannot understand as empty.
internal static class FolderFormat
{
    public const int Current = 3;

    private const int First = 1;

    // Before anything in the folder is read or written; moves an older format on to this one.
    public static void Check(FolderLayout layout)
    {
        var found = Read(layout);
        if (found > Current)
        {
            throw new InvalidOperationException(
                $"The memory in {layout.Root} was written by a newer version of Filum (format {found}); this one reads up to format {Current}. " +
                "Update Filum to open it. Nothing was read or changed.");
        }

        if (found < Current)
        {
            Directory.CreateDirectory(layout.Temporary);
            AtomicFile.Write(layout, layout.FormatFile, Current.ToString(CultureInfo.InvariantCulture) + "\n");
        }
    }

    private static int Read(FolderLayout layout) =>
        File.Exists(layout.FormatFile) && int.TryParse(File.ReadAllText(layout.FormatFile, Encoding.UTF8).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var format)
            ? format
            : First;
}
