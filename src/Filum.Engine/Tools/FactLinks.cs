using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Facts;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Tools;

// A question that chains facts ("the spouse of the author of Book Seven") needs a second lookup for every step; a small
// model often stops after the first. So the facts shown come with those of the subjects their values name, one step on.
internal static class FactLinks
{
    public const int MaxSubjects = 5;
    public const int MaxFactsPerSubject = 10;

    // Of the subjects a query names.
    private const int MaxNamedSubjects = 3;

    // A question in words names a subject ("where was River Okafor born"), not the attribute as it was recorded ("birth
    // city"): the facts that hold now about every subject the query names, and one step on. Empty when it names none,
    // so a search of a memory without facts reads as before.
    public static string About(IReadOnlyList<Fact> current, string query)
    {
        var named = NamedSubjects(current, query);
        var sections = named.Select(facts =>
            $"Facts that hold now about {facts[0].Subject} ({ToolNames.FactsHistory} gives what held before):\n" +
            string.Join('\n', facts.Select(f => $"- {f.Subject} · {f.Attribute}: {f.Value} (since {f.ValidFrom})"))).ToList();
        if (Text(current, named.SelectMany(f => f).ToList()) is { Length: > 0 } links)
        {
            sections.Add(links);
        }

        return string.Join("\n\n", sections);
    }

    // The facts shown about the subjects a query names.
    public static IReadOnlyList<Fact> Named(IReadOnlyList<Fact> current, string query) => [.. NamedSubjects(current, query).SelectMany(f => f)];

    // The same fact read twice: a fact's sources are a list, so records of two reads are not equal.
    public static bool Same(Fact a, Fact b) =>
        FactTable.Same(a.Subject, b.Subject) && FactTable.Same(a.Attribute, b.Attribute) && a.Value == b.Value && a.ValidFrom == b.ValidFrom;

    // The facts that may answer a query without naming their subject (spec 042), each with where it came from.
    public static string Matching(IReadOnlyList<Fact> facts) => facts.Count == 0 ? string.Empty
        : $"Facts that may answer it ({ToolNames.FactsHistory} gives what held before):\n" + string.Join('\n', facts.Select(FactTable.Line));

    private static List<List<Fact>> NamedSubjects(IReadOnlyList<Fact> current, string query)
    {
        var queryWords = MemoryText.WordsOf(query);
        bool Named(string name) => MemoryText.WordsOf(name) is { Count: > 0 } words && words.IsSubsetOf(queryWords);
        return current
            .GroupBy(f => f.Subject, StringComparer.OrdinalIgnoreCase)
            .Where(g => Named(g.Key) || g.Any(f => FactTable.Same(f.Attribute, FactTable.Alias) && Named(f.Value)))
            .Take(MaxNamedSubjects)
            .Select(g => g.Take(MaxFactsPerSubject).ToList())
            .ToList();
    }

    // Empty when no value names a subject with facts that are not already shown.
    public static string Text(IReadOnlyList<Fact> current, IReadOnlyCollection<Fact> shown)
    {
        var shownSubjects = shown.Select(f => f.Subject.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bySubject = current
            .GroupBy(f => f.Subject.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sections = new List<string>();
        foreach (var fact in shown)
        {
            var value = fact.Value.Trim();
            if (sections.Count == MaxSubjects)
            {
                break;
            }

            if (shownSubjects.Contains(value) || !bySubject.TryGetValue(value, out var facts) || !linked.Add(value))
            {
                continue;
            }

            sections.Add($"Facts that hold now about {facts[0].Subject} (the value of {fact.Subject} · {fact.Attribute}):\n" +
                string.Join('\n', facts.Take(MaxFactsPerSubject).Select(FactTable.Line)));
        }

        return string.Join("\n\n", sections);
    }
}
