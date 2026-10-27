using Filum.Engine.Packs.Contracts;

namespace Filum.Engine.Packs.FileSystem;

internal sealed class FileSystemPackSource : IPackSource
{
    private const string ManifestFile = "pack.json";
    private const string PromptFile = "prompt.md";
    private const string SkillsFolder = "skills";
    private const string MemoryFolder = "memory";
    private const string AllFiles = "*";

    public PackContent? Read(string location)
    {
        if (!Directory.Exists(location))
        {
            return null;
        }

        var memory = Path.Combine(location, MemoryFolder);
        return new PackContent(
            ReadText(Path.Combine(location, ManifestFile)),
            ReadText(Path.Combine(location, PromptFile)),
            Files(Path.Combine(location, SkillsFolder), SearchOption.TopDirectoryOnly).Select(f => (Path.GetFileName(f), File.ReadAllText(f))).ToList(),
            Files(memory, SearchOption.AllDirectories).Select(f => ("/" + Path.GetRelativePath(memory, f).Replace('\\', '/'), File.ReadAllText(f))).ToList());
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static IEnumerable<string> Files(string folder, SearchOption depth) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, AllFiles, depth).Where(f => !IsHidden(f)).Order(StringComparer.Ordinal)
            : [];

    // Files like .DS_Store that editors and systems leave behind are not part of a package.
    private static bool IsHidden(string path) => Path.GetFileName(path).StartsWith('.');
}
