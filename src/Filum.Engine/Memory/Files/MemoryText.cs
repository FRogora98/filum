namespace Filum.Engine.Memory.Files;

/// <summary>The text rules of memory files that a store needs too: what a line is.</summary>
public static class MemoryText
{
    private const string WindowsLineBreak = "\r\n";
    private const string LineBreak = "\n";

    /// <summary>The lines of a text; a final line break does not start another line.</summary>
    public static IReadOnlyList<string> LinesOf(string content)
    {
        if (content.Length == 0)
        {
            return [];
        }

        var lines = content.Split('\n');
        return content.EndsWith('\n') ? lines[..^1] : lines;
    }

    // Words that say nothing about what is looked for; a question's are left out when its words are matched.
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "did", "do", "does", "for", "from", "has", "have", "how", "i", "in", "is",
        "it", "its", "me", "my", "of", "on", "or", "that", "the", "this", "to", "was", "were", "what", "when", "where", "which",
        "who", "whom", "with",
    };

    /// <summary>
    /// The words of a text to match a query by, in any order: lower case, split at anything that is not a letter or a
    /// digit, a plural reduced to its singular ("cities" → "city", "rows" → "row").
    /// </summary>
    internal static HashSet<string> WordsOf(string text) =>
        Split(text).Select(Singular).ToHashSet(StringComparer.Ordinal);

    /// <summary>The words of a query that say what is looked for: <see cref="WordsOf"/> without the stopwords.</summary>
    internal static HashSet<string> QueryWordsOf(string query) =>
        Split(query).Where(w => !Stopwords.Contains(w)).Select(Singular).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> Split(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var inWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                yield return text[start..i].ToLowerInvariant();
                start = -1;
            }
        }
    }

    private static string Singular(string word) =>
        word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal) ? word[..^3] + "y"
        : word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1]
        : word;

    internal static string Normalize(string? text) => (text ?? string.Empty).Replace(WindowsLineBreak, LineBreak);

    internal static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    internal static string ReplaceOnce(string text, string oldValue, string newValue)
    {
        var index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return text[..index] + newValue + text[(index + oldValue.Length)..];
    }
}
