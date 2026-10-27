using System.Text.RegularExpressions;

namespace Filum.Evals.LongMemEval.Systems;

// Okapi BM25 with the usual k1 = 1.2 and b = 0.75; words are lower-case letters and digits.
internal static partial class Bm25
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    // k1 + 1, written as the number it is.
    private const double ScoreScale = 2.2;

    private const double IdfSmoothing = 0.5;

    public static IReadOnlyList<int> Top(IReadOnlyList<string> documents, string query, int k)
    {
        var docs = documents.Select(Words).ToList();
        var average = docs.Count == 0 ? 0 : docs.Average(d => d.Count);
        var terms = Words(query).Distinct().ToList();
        var frequency = terms.ToDictionary(t => t, t => docs.Count(d => d.Contains(t)));
        return docs
            .Select((d, i) => (Index: i, Score: terms.Sum(t =>
            {
                var tf = d.Count(w => w == t);
                if (tf == 0)
                {
                    return 0.0;
                }

                var idf = Math.Log(1 + (docs.Count - frequency[t] + IdfSmoothing) / (frequency[t] + IdfSmoothing));
                return idf * tf * ScoreScale / (tf + K1 * (1 - B + B * d.Count / Math.Max(1, average)));
            })))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Index)
            .Take(k)
            .Select(s => s.Index)
            .ToList();
    }

    public static List<string> Words(string text) => Word().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
