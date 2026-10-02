using System.Text.RegularExpressions;

namespace Filum.Engine;

/// <summary>What an event of the log is (spec 030).</summary>
public static class MemoryEventKind
{
    /// <summary>A message of the person.</summary>
    public const string Said = "said";

    /// <summary>The agent's answer.</summary>
    public const string Answered = "answered";

    /// <summary>An item from a file or, later, a connector.</summary>
    public const string Imported = "imported";

    /// <summary>An explicit instruction ("from now on…"), recorded when a turn changed the core's rules.</summary>
    public const string Told = "told";

    /// <summary>The person changed, deleted or forgot something.</summary>
    public const string Corrected = "corrected";

    /// <summary>A change to the memory's files, with the events it came from and the revisions it made.</summary>
    public const string Derived = "derived";

    /// <summary>A structural change consolidation suggests, waiting for the person's answer.</summary>
    public const string Proposed = "proposed";

    /// <summary>What was said or brought in: the events a search and a consolidation read.</summary>
    public static readonly IReadOnlyList<string> Spoken = [Said, Answered, Imported];
}

/// <summary>Where an event comes from.</summary>
public static class MemoryEventSource
{
    public const string Chat = "chat";
    public const string Import = "import";
    public const string App = "app";
    public const string Agent = "agent";
    public const string Consolidation = "consolidation";

    /// <summary>An agent that hosts the engine through MCP, recording what the person said (<c>memory_log</c>).</summary>
    public const string Host = "host";
}

/// <summary>One event of a person's log: what happened, when, where it came from. Never rewritten.</summary>
public sealed record MemoryEvent(
    long Id,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    string Kind,
    string Source,
    Guid? ConversationId,
    Guid? MessageId,
    string Text,
    string Sensitivity,
    IReadOnlyList<long> Sources,
    IReadOnlyList<long> Revisions);

/// <summary>An event to append; the store gives it its id.</summary>
public sealed record NewMemoryEvent(
    string Kind,
    string Source,
    string Text,
    DateTimeOffset? OccurredAt = null,
    Guid? ConversationId = null,
    Guid? MessageId = null,
    string Sensitivity = MemorySensitivity.Normal,
    IReadOnlyList<long>? Sources = null,
    IReadOnlyList<long>? Revisions = null);

/// <summary>Helpers for stores.</summary>
public static class MemoryEvents
{
    /// <summary>The event a store saves for <paramref name="next"/>, with its id and recording time.</summary>
    public static MemoryEvent From(long id, NewMemoryEvent next, DateTimeOffset now) =>
        new(id, next.OccurredAt ?? now, now, next.Kind, next.Source, next.ConversationId, next.MessageId, next.Text, next.Sensitivity,
            next.Sources ?? [], next.Revisions ?? []);
}

/// <summary>Which events to read: all of them by default, oldest first.</summary>
public sealed record EventQuery(
    long? AfterId = null,
    IReadOnlyCollection<string>? Kinds = null,
    Guid? ConversationId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? Before = null,
    bool IncludePrivate = true,
    Guid? MessageId = null)
{
    public bool Matches(MemoryEvent e) =>
        (AfterId is null || e.Id > AfterId)
        && (Kinds is null || Kinds.Contains(e.Kind))
        && (ConversationId is null || e.ConversationId == ConversationId)
        && (MessageId is null || e.MessageId == MessageId)
        && (From is null || e.OccurredAt >= From)
        && (Before is null || e.OccurredAt < Before)
        && (IncludePrivate || e.Sensitivity != MemorySensitivity.Private);
}

/// <summary>A found event, with the one that followed it in its conversation when there is one.</summary>
public sealed record MemoryEventHit(MemoryEvent Event, MemoryEvent? Next, double Score);

/// <summary>BM25 over the words of events: a search with no index and no model.</summary>
public static partial class EventSearch
{
    public static IReadOnlyList<MemoryEventHit> Rank(IReadOnlyList<MemoryEvent> events, string query, int max)
    {
        var terms = Words(query).Distinct().ToList();
        if (events.Count == 0 || terms.Count == 0)
        {
            return [];
        }

        var docs = events.Select(e => Words(e.Text)).ToList();
        var average = docs.Average(d => d.Count);
        var frequency = terms.ToDictionary(t => t, t => docs.Count(d => d.Contains(t)));
        return docs
            .Select((d, i) => (Index: i, Score: terms.Sum(t => Bm25(d, t, frequency[t], docs.Count, average))))
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

    /// <summary>The start of a long text (where its context often is) and the part around the first matching word.</summary>
    public static string Excerpt(string content, string query, int window = 500)
    {
        if (content.Length <= 2 * window)
        {
            return content;
        }

        var lower = content.ToLowerInvariant();
        var at = Words(query).Select(t => lower.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, at - window / 2);
        return start <= 200 ? Cut(content, 2 * window) : $"{Cut(content, 200)}\n[…]\n{content.Substring(start, Math.Min(window, content.Length - start))}…";
    }

    public static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private static double Bm25(List<string> doc, string term, int docsWithTerm, int docs, double average)
    {
        var tf = doc.Count(w => w == term);
        if (tf == 0)
        {
            return 0;
        }

        var idf = Math.Log(1 + (docs - docsWithTerm + 0.5) / (docsWithTerm + 0.5));
        return idf * tf * 2.2 / (tf + 1.2 * (1 - 0.75 + 0.75 * doc.Count / Math.Max(1, average)));
    }

    private static List<string> Words(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
