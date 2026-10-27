using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Memory;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory.Files;
using Filum.Engine.Packs.Contracts;
using Filum.Engine.Packs.FileSystem;
using Filum.Engine.Skills;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Packs;

/// <summary>Reads a package from its folder and checks it with the engine's own rules before anything uses it.</summary>
public static class PackLoader
{
    // What a package without a manifest is called while its other problems are found.
    private const string UnnamedPack = "unnamed";
    private const string UnknownVersion = "0";

    private static readonly IPackSource Folders = new FileSystemPackSource();

    /// <summary>
    /// Reads the package in <paramref name="folder"/> and checks it with the engine's own rules, by applying it to an
    /// empty memory first. Throws <see cref="PackException"/> with every problem found; nothing of a refused package is used.
    /// </summary>
    public static async Task<Pack> LoadAsync(string folder, MemoryOptions limits, CancellationToken cancellationToken)
    {
        var loaded = await TryLoadAsync(folder, limits, cancellationToken);
        return loaded.IsLoaded ? loaded.Pack : throw new PackException(folder, loaded.Problems);
    }

    /// <summary>
    /// Reads and checks the package in <paramref name="folder"/> as <see cref="LoadAsync"/> does, and returns a refused
    /// package as a result, with every problem found, instead of throwing.
    /// </summary>
    public static Task<PackLoadResult> TryLoadAsync(string folder, MemoryOptions limits, CancellationToken cancellationToken) =>
        TryLoadFromAsync(Folders, folder, limits, cancellationToken);

    internal static async Task<PackLoadResult> TryLoadFromAsync(IPackSource source, string folder, MemoryOptions limits, CancellationToken cancellationToken)
    {
        if (source.Read(folder) is not { } content)
        {
            return PackLoadResult.Refused(folder, [$"There is no folder at {Path.GetFullPath(folder)}."]);
        }

        var problems = new List<string>();
        var manifest = PackManifest.Read(content.Manifest, problems);
        var prompt = content.Prompt is null ? null : Normalized(content.Prompt);
        if (prompt is not null && prompt.Length > limits.MaxCoreChars)
        {
            problems.Add($"prompt.md is {prompt.Length} characters, over the limit of {limits.MaxCoreChars}.");
        }

        var skills = Skills(content.Skills, problems);
        var (core, files) = MemoryFiles(content.Memory, manifest, problems);
        // The trial runs even after problems above, on what could be read, so every problem is reported at once.
        var pack = new Pack(manifest?.Name ?? UnnamedPack, manifest?.Version ?? UnknownVersion, manifest?.Description ?? string.Empty,
            string.IsNullOrWhiteSpace(prompt) ? null : prompt.Trim(), core, skills, files);
        problems.AddRange(await TrialAsync(pack, limits, cancellationToken));
        return problems.Count > 0 ? PackLoadResult.Refused(folder, problems) : PackLoadResult.Loaded(pack);
    }

    // On a scratch store, with the very rules of a new memory.
    private static Task<IReadOnlyList<string>> TrialAsync(Pack pack, MemoryOptions limits, CancellationToken cancellationToken)
    {
        var store = new PackTrialStore();
        var options = Options.Create(limits);
        var writer = new MemoryWriter(store, options, NullLogger<MemoryWriter>.Instance, TimeProvider.System);
        var bootstrapper = new CoreBootstrapper(store, writer, new SkillService(store, writer), new EnginePack(pack), NullLogger<CoreBootstrapper>.Instance);
        return bootstrapper.CreateMemoryAsync(Guid.NewGuid(), cancellationToken);
    }

    private static List<Skill> Skills(IReadOnlyList<(string Name, string Content)> files, List<string> problems)
    {
        var skills = new List<Skill>();
        foreach (var (file, text) in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var content = Normalized(text);
            // The length limit is checked by the trial run, with the host's limits.
            var error = Path.GetExtension(file) != EngineConstants.FileExtensions.Document ? "a skill file ends in .md." : SkillFormat.CheckFile(SkillFormat.PathOf(name), content, int.MaxValue);
            if (error is not null)
            {
                problems.Add($"skills/{file}: {error}");
                continue;
            }

            if (SkillFormat.Parse(content).Skill is { } skill)
            {
                skills.Add(skill);
            }
        }

        return skills;
    }

    private static (string? Core, IReadOnlyList<PackFile> Files) MemoryFiles(IReadOnlyList<(string Path, string Content)> entries, PackManifest? manifest, List<string> problems)
    {
        string? core = null;
        var files = new List<PackFile>();
        foreach (var (path, content) in entries)
        {
            if (MemoryPaths.IsCore(path))
            {
                core = Normalized(content);
            }
            else if (SkillFormat.IsSkillPath(path))
            {
                problems.Add($"memory{path}: skills go in the skills folder of the package, not in memory/skills.");
            }
            else
            {
                var sensitivity = manifest?.Private.Contains(path) == true ? MemorySensitivity.Private
                    : manifest?.Sensitive.Contains(path) == true ? MemorySensitivity.Sensitive
                    : MemorySensitivity.Normal;
                files.Add(new PackFile(path, Normalized(content), sensitivity));
            }
        }

        foreach (var listed in (manifest?.Private ?? []).Concat(manifest?.Sensitive ?? []).Where(p => files.All(f => f.Path != p)))
        {
            problems.Add($"pack.json marks {listed}, which is not a file of memory/.");
        }

        return (core, files);
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n");
}
