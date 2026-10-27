using System.Text;
using System.Text.Json;

namespace Filum.Engine.Storage.LocalFolder;

internal static class JsonLines
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    // A line a write cut short is skipped.
    public static List<T> Read<T>(string path) => File.Exists(path) ? Parse<T>(File.ReadLines(path)) : [];

    public static List<T> Parse<T>(IEnumerable<string> lines)
    {
        var items = new List<T>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<T>(line, Options) is { } item)
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // A line a crash cut short: the rest of the log is still read.
            }
        }

        return items;
    }

    // Flushed to disk; a line a crash cut short is closed first.
    public static void Append(string path, string line)
    {
        using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        if (file.Length > 0 && LastByte(path) != (byte)'\n')
        {
            file.WriteByte((byte)'\n');
        }

        file.Write(Encoding.UTF8.GetBytes(line + "\n"));
        file.Flush(flushToDisk: true);
    }

    // Lines of the old file that do not parse were never read: they are kept in a file next to it (.unreadable) rather
    // than lost.
    public static void Rewrite<T>(FolderLayout layout, string path, IEnumerable<string> lines)
    {
        var unreadable = File.Exists(path) ? File.ReadLines(path).Where(l => l.Length > 0 && !Parses<T>(l)).ToList() : [];
        if (unreadable.Count > 0)
        {
            File.AppendAllText(path + FolderLayout.Names.UnreadableSuffix, string.Concat(unreadable.Select(l => l + "\n")), new UTF8Encoding(false));
        }

        AtomicFile.Write(layout, path, string.Concat(lines.Select(l => l + "\n")));
    }

    private static bool Parses<T>(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(line, Options) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte LastByte(string path)
    {
        using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        read.Seek(-1, SeekOrigin.End);
        return (byte)read.ReadByte();
    }
}
