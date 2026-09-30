using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine;

/// <summary>A file a package puts in every new memory.</summary>
public sealed record PackFile(string Path, string Content, string Sensitivity);

/// <summary>
/// A package (spec 016): the data that makes the engine vertical, the same for every person. Its prompt is read at every
/// turn; its core, files and skills are given to a memory when it is created. Generic: the format knows no domain.
/// </summary>
public sealed partial record Pack(
    string Name, string Version, string Description, string? Prompt, string? Core, IReadOnlyList<Skill> Skills, IReadOnlyList<PackFile> Files)
{
    /// <summary>The version of the package format this engine reads.</summary>
    public const int Format = 1;

    private static readonly string[] Fields = ["format", "name", "version", "description", "private", "sensitive"];

    /// <summary>
    /// Reads the package in <paramref name="folder"/> and checks it with the engine's own rules, by applying it to an
    /// empty memory first. Throws <see cref="PackException"/> with every problem found; nothing of a refused package is used.
    /// </summary>
    public static Pack Load(string folder, MemoryOptions limits)
    {
        var problems = new List<string>();
        if (!Directory.Exists(folder))
        {
            throw new PackException(folder, [$"There is no folder at {Path.GetFullPath(folder)}."]);
        }

        var manifest = ReadManifest(Path.Combine(folder, "pack.json"), problems);
        var prompt = ReadText(Path.Combine(folder, "prompt.md"));
        if (prompt is not null && prompt.Length > limits.MaxCoreChars)
        {
            problems.Add($"prompt.md is {prompt.Length} characters, over the limit of {limits.MaxCoreChars}.");
        }

        var skills = ReadSkills(Path.Combine(folder, "skills"), problems);
        var (core, files) = ReadMemory(Path.Combine(folder, "memory"), manifest, problems);
        // The trial runs even after problems above, on what could be read, so every problem is reported at once.
        var pack = new Pack(manifest?.Name ?? "unnamed", manifest?.Version ?? "0", manifest?.Description ?? string.Empty,
            string.IsNullOrWhiteSpace(prompt) ? null : prompt.Trim(), core, skills, files);
        var trial = new MemoryService(new InMemoryMemoryStore(), Options.Create(limits), NullLogger<MemoryService>.Instance, pack: pack);
        problems.AddRange(trial.CreateMemoryAsync(Guid.NewGuid(), CancellationToken.None).GetAwaiter().GetResult());
        return problems.Count > 0 ? throw new PackException(folder, problems) : pack;
    }

    private sealed record Manifest(string Name, string Version, string Description, IReadOnlyList<string> Private, IReadOnlyList<string> Sensitive);

    private static Manifest? ReadManifest(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            problems.Add("pack.json is missing.");
            return null;
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            root = document.RootElement.Clone();
        }
        catch (JsonException e)
        {
            problems.Add($"pack.json is not valid JSON: {e.Message}");
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            problems.Add("pack.json must be an object.");
            return null;
        }

        var before = problems.Count;
        foreach (var unknown in root.EnumerateObject().Select(p => p.Name).Where(n => !Fields.Contains(n)))
        {
            problems.Add($"pack.json has the field '{unknown}', which format {Format} does not know.");
        }

        if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number))
        {
            problems.Add($"pack.json needs \"format\": {Format}.");
        }
        else if (number != Format)
        {
            problems.Add($"pack.json has format {number}; this engine reads format {Format}.");
        }

        var name = Text(root, "name", problems);
        if (name is not null && !AllowedName().IsMatch(name))
        {
            problems.Add($"The package name '{name}' must be lowercase words joined by '-'.");
        }

        var version = Text(root, "version", problems);
        var description = Text(root, "description", problems);
        var @private = Paths(root, "private", problems);
        var sensitive = Paths(root, "sensitive", problems);
        return problems.Count > before ? null : new Manifest(name!, version!, description!, @private, sensitive);
    }

    private static string? Text(JsonElement root, string field, List<string> problems)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!.Trim();
        }

        problems.Add($"pack.json needs a \"{field}\" text.");
        return null;
    }

    private static IReadOnlyList<string> Paths(JsonElement root, string field, List<string> problems)
    {
        if (!root.TryGetProperty(field, out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))
        {
            problems.Add($"\"{field}\" in pack.json must be a list of paths.");
            return [];
        }

        return value.EnumerateArray().Select(v => v.GetString()!).ToList();
    }

    private static IReadOnlyList<Skill> ReadSkills(string folder, List<string> problems)
    {
        var skills = new List<Skill>();
        if (!Directory.Exists(folder))
        {
            return skills;
        }

        foreach (var file in Directory.EnumerateFiles(folder).Where(f => !IsHidden(f)).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var content = ReadText(file)!;
            // The length limit is checked by the trial run, with the host's limits.
            var error = Path.GetExtension(file) != ".md" ? "a skill file ends in .md." : Engine.Skills.CheckFile(Engine.Skills.PathOf(name), content, int.MaxValue);
            if (error is not null)
            {
                problems.Add($"skills/{Path.GetFileName(file)}: {error}");
                continue;
            }

            skills.Add(Engine.Skills.Parse(content).Skill!);
        }

        return skills;
    }

    private static (string? Core, IReadOnlyList<PackFile> Files) ReadMemory(string folder, Manifest? manifest, List<string> problems)
    {
        string? core = null;
        var files = new List<PackFile>();
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(f => !IsHidden(f)).Order(StringComparer.Ordinal))
            {
                var path = "/" + Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (MemoryPaths.IsCore(path))
                {
                    core = ReadText(file);
                }
                else if (Engine.Skills.IsSkillPath(path))
                {
                    problems.Add($"memory{path}: skills go in the skills folder of the package, not in memory/skills.");
                }
                else
                {
                    var sensitivity = manifest?.Private.Contains(path) == true ? MemorySensitivity.Private
                        : manifest?.Sensitive.Contains(path) == true ? MemorySensitivity.Sensitive
                        : MemorySensitivity.Normal;
                    files.Add(new PackFile(path, ReadText(file)!, sensitivity));
                }
            }
        }

        foreach (var listed in (manifest?.Private ?? []).Concat(manifest?.Sensitive ?? []).Where(p => files.All(f => f.Path != p)))
        {
            problems.Add($"pack.json marks {listed}, which is not a file of memory/.");
        }

        return (core, files);
    }

    private static string? ReadText(string path) =>
        File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;

    /// <summary>Files like .DS_Store that editors and systems leave behind are not part of a package.</summary>
    private static bool IsHidden(string path) => Path.GetFileName(path).StartsWith('.');

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex AllowedName();
}

/// <summary>A package that cannot be used, with every problem found in it.</summary>
public sealed class PackException(string folder, IReadOnlyList<string> problems)
    : Exception($"The package in {folder} cannot be used:\n" + string.Join("\n", problems.Select(p => "- " + p)))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
