using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.History;

// So a reader sees the old value beside the new one even when the file was overwritten in place. Each added line is
// paired with the most similar removed line, when similar enough to be the same entry.
internal static class LineChanges
{
    public const int MaxNote = 200;

    // Below this share of common words, an added line is a new entry, not a removed one rewritten.
    private const double MinSimilarity = 0.3;

    // versions are oldest first.
    public static IReadOnlyDictionary<string, LineChange> Of(IReadOnlyList<(string Content, DateTimeOffset At)> versions)
    {
        var changes = new Dictionary<string, LineChange>(StringComparer.Ordinal);
        for (var v = 1; v < versions.Count; v++)
        {
            var before = Lines(versions[v - 1].Content);
            var after = Lines(versions[v].Content);
            var removed = Minus(before, after);
            var added = Minus(after, before);
            foreach (var line in added)
            {
                var match = removed
                    .Select(old => (Old: old, Score: Similarity(old, line)))
                    .Where(m => m.Score >= MinSimilarity)
                    .OrderByDescending(m => m.Score)
                    .FirstOrDefault();
                if (match.Old is not null)
                {
                    changes[line] = new LineChange(Difference(match.Old, line), versions[v].At);
                    removed.Remove(match.Old);
                }
            }

            // A line that is gone stops carrying its note; one that stayed keeps it.
            foreach (var key in changes.Keys.Where(k => !after.Contains(k)).ToList())
            {
                changes.Remove(key);
            }
        }

        return changes;
    }

    private static List<string> Lines(string content) =>
        content.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();

    // As many times as they are missing.
    private static List<string> Minus(List<string> a, List<string> b)
    {
        var left = b.GroupBy(l => l, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var line in a)
        {
            if (left.TryGetValue(line, out var n) && n > 0)
            {
                left[line] = n - 1;
            }
            else
            {
                result.Add(line);
            }
        }

        return result;
    }

    // Rows with the same first field are the same entry.
    private static double Similarity(string a, string b)
    {
        if (a.Contains(',') && b.Contains(',') && a.Split(',')[0].Trim() is { Length: > 0 } key && key == b.Split(',')[0].Trim())
        {
            return 1;
        }

        var wa = Words(a).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wb = Words(b).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var union = wa.Union(wb, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)wa.Intersect(wb, StringComparer.OrdinalIgnoreCase).Count() / union;
    }

    private static string[] Words(string line) => line.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);

    // With one word around the differing ones.
    private static string Difference(string old, string now)
    {
        var o = old.Split(' ');
        var n = now.Split(' ');
        var start = 0;
        while (start < o.Length && start < n.Length && o[start] == n[start])
        {
            start++;
        }

        var end = 0;
        while (end < o.Length - start && end < n.Length - start && o[^(end + 1)] == n[^(end + 1)])
        {
            end++;
        }

        var from = Math.Max(0, start - 1);
        var to = Math.Min(o.Length, o.Length - end + 1);
        var part = string.Join(' ', o[from..to]);
        var text = (from > 0 ? "…" : string.Empty) + part + (to < o.Length ? "…" : string.Empty);
        return text.Length <= MaxNote ? text : text[..MaxNote] + "…";
    }
}
