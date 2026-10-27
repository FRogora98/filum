using System.Text.RegularExpressions;

namespace Filum.Evals.Checks;

// Case, typographic quotes and thousand separators do not count.
internal static partial class ComparableText
{
    // "2,396", "2.396" and "2 396" all read "2396"; "don’t" reads "don't".
    public static string Normalize(string text) =>
        ThousandSeparator().Replace(text.ToLowerInvariant().Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"'), string.Empty);

    public static bool Contains(string text, string value) => Normalize(text).Contains(Normalize(value), StringComparison.Ordinal);

    public static bool ContainsAny(string text, IReadOnlyList<string> values)
    {
        var haystack = Normalize(text);
        return values.Any(v => haystack.Contains(Normalize(v), StringComparison.Ordinal));
    }

    public static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"(?<=\d)[.,'   ](?=\d{3}(?!\d))")]
    private static partial Regex ThousandSeparator();
}
