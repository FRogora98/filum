using System.Text.RegularExpressions;

namespace Filum.Engine.Skills;

/// <summary>A skill the person invokes by name at the start of a message, and the names closest to one that does not exist.</summary>
public static partial class SkillInvocation
{
    private const int DefaultClosestCount = 3;

    /// <summary>
    /// A message that invokes a skill by name: <c>/name</c> at its very start, then a space or the end. Returns the name
    /// and the rest of the message, or null.
    /// </summary>
    public static (string Name, string Input)? Of(string message)
    {
        var match = InvocationPattern().Match(message);
        return match.Success ? (match.Groups[1].Value, message[match.Length..].Trim()) : null;
    }

    /// <summary>The names closest to <paramref name="name"/>: first those that share its start, then by edit distance.</summary>
    public static IReadOnlyList<string> Closest(string name, IEnumerable<string> names, int count = DefaultClosestCount) =>
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

    [GeneratedRegex(@"^\s*/([a-z0-9]+(?:-[a-z0-9]+)*)(?=\s|$)")]
    private static partial Regex InvocationPattern();
}
