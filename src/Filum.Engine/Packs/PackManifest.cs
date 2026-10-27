using System.Text.Json;
using System.Text.RegularExpressions;

namespace Filum.Engine.Packs;

internal sealed partial record PackManifest(string Name, string Version, string Description, IReadOnlyList<string> Private, IReadOnlyList<string> Sensitive)
{
    private const string FormatField = "format";
    private const string NameField = "name";
    private const string VersionField = "version";
    private const string DescriptionField = "description";
    private const string PrivateField = "private";
    private const string SensitiveField = "sensitive";

    private static readonly string[] Fields = [FormatField, NameField, VersionField, DescriptionField, PrivateField, SensitiveField];

    // Null, with every problem added, when it cannot be used.
    public static PackManifest? Read(string? json, List<string> problems)
    {
        if (ReadObject(json, problems) is not { } root)
        {
            return null;
        }

        var before = problems.Count;
        foreach (var unknown in root.EnumerateObject().Select(p => p.Name).Where(n => !Fields.Contains(n)))
        {
            problems.Add($"pack.json has the field '{unknown}', which format {Pack.Format} does not know.");
        }

        if (!root.TryGetProperty(FormatField, out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number))
        {
            problems.Add($"pack.json needs \"format\": {Pack.Format}.");
        }
        else if (number != Pack.Format)
        {
            problems.Add($"pack.json has format {number}; this engine reads format {Pack.Format}.");
        }

        var name = Text(root, NameField, problems);
        if (name is not null && !AllowedName().IsMatch(name))
        {
            problems.Add($"The package name '{name}' must be lowercase words joined by '-'.");
        }

        var version = Text(root, VersionField, problems);
        var description = Text(root, DescriptionField, problems);
        var @private = Paths(root, PrivateField, problems);
        var sensitive = Paths(root, SensitiveField, problems);
        return problems.Count > before || name is null || version is null || description is null
            ? null
            : new PackManifest(name, version, description, @private, sensitive);
    }

    // Null, with the problem, when the file is missing, not JSON or not an object.
    private static JsonElement? ReadObject(string? json, List<string> problems)
    {
        if (json is null)
        {
            problems.Add("pack.json is missing.");
            return null;
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
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

        return root;
    }

    private static string? Text(JsonElement root, string field, List<string> problems)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
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

        return value.EnumerateArray().Select(v => v.GetString()).OfType<string>().ToList();
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex AllowedName();
}
