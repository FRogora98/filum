using System.Text;
using System.Text.RegularExpressions;

namespace Filum.Engine;

/// <summary>A procedure the person keeps: a header (name, description, when it applies, on or off) and plain steps.</summary>
public sealed record Skill(string Name, string Description, string When, bool Enabled, string Steps);

/// <summary>A skill as the memory holds it.</summary>
public sealed record SkillFile(Skill Skill, string Path, string Sensitivity, DateTimeOffset UpdatedAt);

/// <summary>
/// The format of a skill file, <c>/skills/&lt;name&gt;.md</c>: a small header block between <c>---</c> lines, then the
/// steps. Everything here is code, so a skill is checked the same way whoever writes it. Generic: the starter skills
/// name no domain.
/// </summary>
public static partial class Skills
{
    public const string Folder = "/skills/";
    public const int MaxNameLength = 40;
    public const int MaxLineLength = 300;

    public static string PathOf(string name) => $"{Folder}{name}.md";

    public static bool IsSkillPath(string? path) => path?.StartsWith(Folder, StringComparison.Ordinal) == true;

    /// <summary>The name a skill path stands for, or null when the path is not <c>/skills/&lt;name&gt;.md</c>.</summary>
    public static string? NameOf(string path)
    {
        if (!IsSkillPath(path) || !path.EndsWith(".md", StringComparison.Ordinal))
        {
            return null;
        }

        var name = path[Folder.Length..^3];
        return ValidateName(name) is null ? name : null;
    }

    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A skill needs a name.";
        }

        if (name.Length > MaxNameLength || !AllowedName().IsMatch(name))
        {
            return $"The skill name '{name}' is not allowed: use lowercase letters and digits in words joined by '-', like 'weekly-review', at most {MaxNameLength} characters.";
        }

        return null;
    }

    /// <summary>What is wrong with a skill before it is written, or null.</summary>
    public static string? Validate(Skill skill)
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

    public static string Format(Skill skill)
    {
        var text = new StringBuilder();
        text.Append("---\n");
        text.Append("name: ").Append(skill.Name.Trim()).Append('\n');
        text.Append("description: ").Append(skill.Description.Trim()).Append('\n');
        text.Append("when: ").Append(skill.When.Trim()).Append('\n');
        text.Append("enabled: ").Append(skill.Enabled ? "true" : "false").Append('\n');
        text.Append("---\n");
        text.Append(skill.Steps.Replace("\r\n", "\n").Trim()).Append('\n');
        return text.ToString();
    }

    /// <summary>A skill file read back; the reason when it is not one.</summary>
    public static (Skill? Skill, string? Error) Parse(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (null, "A skill starts with a header block: a line '---', the lines name, description, when and enabled, then '---'.");
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
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

        var missing = new[] { "name", "description", "when", "enabled" }.Where(f => !fields.ContainsKey(f)).ToList();
        if (missing.Count > 0)
        {
            return (null, $"The skill header has no {string.Join(", ", missing)}.");
        }

        if (!bool.TryParse(fields["enabled"], out var enabled))
        {
            return (null, "In the skill header, enabled is true or false.");
        }

        var skill = new Skill(fields["name"], fields["description"], fields["when"], enabled, string.Join('\n', lines[(end + 1)..]).Trim());
        return Validate(skill) is { } error ? (null, error) : (skill, null);
    }

    /// <summary>What is wrong with the content of a skill file at <paramref name="path"/>, or null.</summary>
    public static string? CheckFile(string path, string content, int maxChars)
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
        if (error is not null)
        {
            return $"/{name} is not a valid skill: {error}";
        }

        return skill!.Name != name ? $"The skill at {path} must be named '{name}' in its header." : null;
    }

    /// <summary>
    /// A message that invokes a skill by name: <c>/name</c> at its very start, then a space or the end. Returns the name
    /// and the rest of the message, or null.
    /// </summary>
    public static (string Name, string Input)? Invocation(string message)
    {
        var match = InvocationPattern().Match(message);
        return match.Success ? (match.Groups[1].Value, message[match.Length..].Trim()) : null;
    }

    /// <summary>The names closest to <paramref name="name"/>: first those that share its start, then by edit distance.</summary>
    public static IReadOnlyList<string> Closest(string name, IEnumerable<string> names, int count = 3) =>
        names
            .OrderBy(n => n.StartsWith(name, StringComparison.Ordinal) || name.StartsWith(n, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(n => Distance(name, n))
            .ThenBy(n => n, StringComparer.Ordinal)
            .Take(count)
            .ToList();

    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }

    /// <summary>Every new person starts with these, created with the core; they are ordinary skills.</summary>
    public static IReadOnlyList<Skill> Starters { get; } =
    [
        new("create-a-skill",
            "Turn something the person asks for again and again into a skill",
            "the person asks to create a skill, or to make something they often ask for quicker",
            true,
            """
            1. Find out what the person wants done each time: what they will give you, what to keep or change in the memory, and what to answer.
            2. Pick a short name of lowercase words joined by '-', a one-line description, and when the skill applies.
            3. Write the steps plainly, using what you can already do: add rows to a collection, update a document, compute totals and counts.
            4. Save it with skill_save, then tell the person in a few words what it does and that they can use it by typing /name or just by asking.
            """),
        new("what-you-know-about-me",
            "A tidy summary of what Filum knows about the person",
            "the person asks what you know or have learned about them",
            true,
            """
            1. Start from the core: who the person is, the rules they gave you, and how they want to be answered.
            2. Go through the list of what the memory holds and say briefly what each part is about; for a collection, how many entries it has. Read a file only when its name is not enough to describe it.
            3. Leave out sensitive and private content unless the person asks for it.
            4. Keep it short and grouped by topic, then ask whether anything is wrong or missing.
            """),
        new("summary-of-a-period",
            "What was saved or changed in the memory over a period",
            "the person asks for a summary of a week, a month or another period",
            true,
            """
            1. Settle the period: the one the person names, or the last 7 days.
            2. From the list of what the memory holds, pick what changed in that period.
            3. For a collection with a date field, count and total the entries of the period with collection_aggregate (dateColumn, from, to). For a document, read the part that changed.
            4. Answer grouped by topic, short, with the exact numbers the tools gave.
            """)
    ];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex AllowedName();

    [GeneratedRegex(@"^\s*/([a-z0-9]+(?:-[a-z0-9]+)*)(?=\s|$)")]
    private static partial Regex InvocationPattern();
}
