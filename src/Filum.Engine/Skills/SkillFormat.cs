using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Text;
using Filum.Engine.Abstractions.Skills;

namespace Filum.Engine.Skills;

/// <summary>
/// The format of a skill file, <c>/skills/&lt;name&gt;.md</c>: a small header block between <c>---</c> lines, then the
/// steps. Everything here is code, so a skill is checked the same way whoever writes it.
/// </summary>
public static partial class SkillFormat
{
    private const string HeaderLine = "---";
    private const string NameField = "name";
    private const string DescriptionField = "description";
    private const string WhenField = "when";
    private const string EnabledField = "enabled";
    private const string FieldSeparator = ": ";
    private const string EnabledValue = "true";
    private const string DisabledValue = "false";

    private static readonly string[] RequiredFields = [NameField, DescriptionField, WhenField, EnabledField];

    internal const string Folder = "/skills/";
    private const string NameRequired = "A skill needs a name.";
    internal const int MaxNameLength = 40;
    internal const int MaxLineLength = 300;

    /// <summary>The path of the skill named <paramref name="name"/>.</summary>
    public static string PathOf(string name) => $"{Folder}{name}{EngineConstants.FileExtensions.Document}";

    /// <summary>Whether <paramref name="path"/> is under the skills folder.</summary>
    public static bool IsSkillPath(string? path) => path?.StartsWith(Folder, StringComparison.Ordinal) == true;

    // Null when the path is not /skills/<name>.md.
    internal static string? NameOf(string path)
    {
        if (!IsSkillPath(path) || !path.EndsWith(EngineConstants.FileExtensions.Document, StringComparison.Ordinal))
        {
            return null;
        }

        var name = path[Folder.Length..^EngineConstants.FileExtensions.Document.Length];
        return ValidateName(name) is null ? name : null;
    }

    internal static bool IsInvalidName([NotNullWhen(false)] string? name, [NotNullWhen(true)] out string? error)
    {
        error = ValidateName(name);
        if (error is not null || name is null)
        {
            error ??= NameRequired;
            return true;
        }

        return false;
    }

    /// <summary>Why <paramref name="name"/> is not an allowed skill name, or null when it is.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return NameRequired;
        }

        if (name.Length > MaxNameLength || !AllowedName().IsMatch(name))
        {
            return $"The skill name '{name}' is not allowed: use lowercase letters and digits in words joined by '-', like 'weekly-review', at most {MaxNameLength} characters.";
        }

        return null;
    }

    internal static string? Validate(Skill skill)
    {
        if (ValidateName(skill.Name) is { } error)
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(skill.Description) || string.IsNullOrWhiteSpace(skill.When))
        {
            return "A skill needs a one-line description and says when it applies.";
        }

        if (skill.Description.Contains('\n') || skill.When.Contains('\n') || skill.Description.Length > MaxLineLength || skill.When.Length > MaxLineLength)
        {
            return $"The description and when of a skill are one line each, at most {MaxLineLength} characters.";
        }

        return string.IsNullOrWhiteSpace(skill.Steps) ? "A skill needs its steps." : null;
    }

    /// <summary>The text of a skill file: its header, then its steps.</summary>
    public static string Format(Skill skill)
    {
        var text = new StringBuilder();
        text.Append(HeaderLine).Append('\n');
        text.Append(NameField).Append(FieldSeparator).Append(skill.Name.Trim()).Append('\n');
        text.Append(DescriptionField).Append(FieldSeparator).Append(skill.Description.Trim()).Append('\n');
        text.Append(WhenField).Append(FieldSeparator).Append(skill.When.Trim()).Append('\n');
        text.Append(EnabledField).Append(FieldSeparator).Append(skill.Enabled ? EnabledValue : DisabledValue).Append('\n');
        text.Append(HeaderLine).Append('\n');
        text.Append(skill.Steps.Replace("\r\n", "\n").Trim()).Append('\n');
        return text.ToString();
    }

    /// <summary>A skill file read back; the reason when it is not one.</summary>
    public static (Skill? Skill, string? Error) Parse(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != HeaderLine)
        {
            return (null, "A skill starts with a header block: a line '---', the lines name, description, when and enabled, then '---'.");
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == HeaderLine)
            {
                end = i;
                break;
            }

            var colon = lines[i].IndexOf(':');
            if (colon <= 0)
            {
                return (null, $"Line {i + 1} of the skill header is not 'field: value'.");
            }

            fields[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim();
        }

        if (end < 0)
        {
            return (null, "The skill header is not closed by a line '---'.");
        }

        var missing = RequiredFields.Where(f => !fields.ContainsKey(f)).ToList();
        if (missing.Count > 0)
        {
            return (null, $"The skill header has no {string.Join(", ", missing)}.");
        }

        if (!bool.TryParse(fields[EnabledField], out var enabled))
        {
            return (null, "In the skill header, enabled is true or false.");
        }

        var skill = new Skill(fields[NameField], fields[DescriptionField], fields[WhenField], enabled, string.Join('\n', lines[(end + 1)..]).Trim());
        return Validate(skill) is { } error ? (null, error) : (skill, null);
    }

    internal static string? CheckFile(string path, string content, int maxChars)
    {
        var name = NameOf(path);
        if (name is null)
        {
            return $"{path}: a skill lives at /skills/<name>.md, with a name of lowercase words joined by '-'.";
        }

        if (content.Length > maxChars)
        {
            return $"The skill /{name} would be {content.Length} characters, over the limit of {maxChars}; keep its steps short.";
        }

        var (skill, error) = Parse(content);
        if (error is not null || skill is null)
        {
            return $"/{name} is not a valid skill: {error}";
        }

        return skill.Name != name ? $"The skill at {path} must be named '{name}' in its header." : null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex AllowedName();
}
