using System.Text.RegularExpressions;
using Filum.Engine.Abstractions.Events;
namespace Filum.Engine.Events;

// BM25: no index and no model.
internal static partial class EventSearch
{
    // BM25's usual settings: term saturation k1, its numerator k1 + 1 (written out so the score keeps its bits), and
    // length normalization b.
    private const double K1 = 1.2;
    private const double K1PlusOne = 2.2;
    private const double B = 0.75;
    private const double IdfSmoothing = 0.5;

    // Reciprocal rank fusion: the usual constant, and how deep each ranking is read.
    private const double FusionK = 60;
    private const int FusionDepth = 50;

    private const int DefaultExcerptWindow = 500;
    private const int ExcerptHeadLength = 200;

    public static IReadOnlyList<MemoryEventHit> Rank(IReadOnlyList<MemoryEvent> events, string query, int max)
    {
        return Scores([.. events.Select(e => e.Text)], query)
            .Select((s, i) => (Index: i, Score: s))
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => events[h.Index].Id)
            .Take(max)
            .Select(h =>
            {
                var hit = events[h.Index];
                var next = hit.ConversationId is null ? null : events.FirstOrDefault(e => e.ConversationId == hit.ConversationId && e.Id > hit.Id);
                return new MemoryEventHit(hit, next, h.Score);
            })
            .ToList();
    }

    // The BM25 score of each text for the query; zero for a text that has none of its words.
    public static double[] Scores(IReadOnlyList<string> texts, string query)
    {
        var terms = Words(query).Distinct().ToList();
        if (texts.Count == 0 || terms.Count == 0)
        {
            return new double[texts.Count];
        }

        var docs = texts.Select(Words).ToList();
        var average = docs.Average(d => d.Count);
        var frequency = terms.ToDictionary(t => t, t => docs.Count(d => d.Contains(t)));
        return [.. docs.Select(d => terms.Sum(t => Bm25(d, t, frequency[t], docs.Count, average)))];
    }

    // Reciprocal rank fusion: an event high in either ranking comes up; events without a vector are ranked by words
    // only.
    public static IReadOnlyList<MemoryEventHit> Fuse(IReadOnlyList<MemoryEvent> events, string query, IReadOnlyDictionary<long, float[]> vectors, float[] asked, int max)
    {
        var score = new Dictionary<long, double>();
        var words = Rank(events, query, FusionDepth);
        for (var r = 0; r < words.Count; r++)
        {
            score[words[r].Event.Id] = score.GetValueOrDefault(words[r].Event.Id) + 1 / (FusionK + r + 1);
        }

        var meaning = events
            .Where(e => vectors.ContainsKey(e.Id))
            .Select(e => (e.Id, Cosine: MemoryVectors.Dot(vectors[e.Id], asked)))
            .OrderByDescending(x => x.Cosine)
            .Take(FusionDepth)
            .ToList();
        for (var r = 0; r < meaning.Count; r++)
        {
            score[meaning[r].Id] = score.GetValueOrDefault(meaning[r].Id) + 1 / (FusionK + r + 1);
        }

        var byId = events.ToDictionary(e => e.Id);
        return score
            .OrderByDescending(s => s.Value)
            .ThenByDescending(s => s.Key)
            .Take(max)
            .Select(s =>
            {
                var hit = byId[s.Key];
                var next = hit.ConversationId is null ? null : events.FirstOrDefault(e => e.ConversationId == hit.ConversationId && e.Id > hit.Id);
                return new MemoryEventHit(hit, next, s.Value);
            })
            .ToList();
    }

    // The start of a long text, where its context often is, and the part around the first matching word.
    public static string Excerpt(string content, string query, int window = DefaultExcerptWindow)
    {
        if (content.Length <= 2 * window)
        {
            return content;
        }

        var lower = content.ToLowerInvariant();
        var at = Words(query).Select(t => lower.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, at - window / 2);
        return start <= ExcerptHeadLength ? Cut(content, 2 * window) : $"{Cut(content, ExcerptHeadLength)}\n[…]\n{content.Substring(start, Math.Min(window, content.Length - start))}…";
    }

    public static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private static double Bm25(List<string> doc, string term, int docsWithTerm, int docs, double average)
    {
        var tf = doc.Count(w => w == term);
        if (tf == 0)
        {
            return 0;
        }

        var idf = Math.Log(1 + (docs - docsWithTerm + IdfSmoothing) / (docsWithTerm + IdfSmoothing));
        return idf * tf * K1PlusOne / (tf + K1 * (1 - B + B * doc.Count / Math.Max(1, average)));
    }

    private static List<string> Words(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
