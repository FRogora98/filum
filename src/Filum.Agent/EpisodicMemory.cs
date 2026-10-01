using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Filum.Agent;

/// <summary>
/// The person's past conversations as a second memory (spec 029): the files are what the agent chose to keep, the
/// conversations are what was said, with its date. <c>conversation_search</c> finds the best-matching past messages
/// (BM25) of the person's other conversations, each with its date, its conversation's title and the turn around it.
/// </summary>
public static partial class EpisodicMemory
{
    public const string ToolName = "conversation_search";

    /// <summary>The rule the instructions gain when the tool is offered.</summary>
    public const string Rule = """

        # Past conversations
        - The files are what you chose to keep; every past conversation with the person is also kept, as it was said. When a question needs a detail, a date, a number, or what exactly was said, search the past conversations with conversation_search, and answer from what you find. When the files and a conversation disagree, the newer one wins: say which you used.
        """;

    private const int MaxHits = 8;
    private const int Window = 500;

    public static AIFunction Tool(DbContext db, Guid person, Guid currentConversation, MemoryTools steps) =>
        AIFunctionFactory.Create(
            async ([Description("Words to look for in what was said, for example a name, a thing or an event.")] string query,
                [Description("Only messages on or after this day, yyyy-MM-dd.")] string? from = null,
                [Description("Only messages on or before this day, yyyy-MM-dd.")] string? to = null,
                CancellationToken cancellationToken = default) =>
            {
                var clock = Stopwatch.StartNew();
                var text = await SearchAsync(db, person, currentConversation, query, Day(from), Day(to)?.AddDays(1), cancellationToken);
                steps.Record(new ToolStep(ToolStep.Searched, ToolName, null, $"Searched past conversations for \"{query}\"", (int)clock.ElapsedMilliseconds, null));
                return text;
            },
            ToolName,
            "Search the person's past conversations: what was said, with its date and the turn around it. Use it for details, dates, numbers and exact words that the memory files may not have kept.");

    public static async Task<string> SearchAsync(DbContext db, Guid person, Guid currentConversation, string query, DateTimeOffset? from, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var conversations = db.Set<Conversation>().Where(c => c.UserId == person && c.Id != currentConversation);
        var messages = await db.Set<ConversationMessage>()
            .Where(m => conversations.Select(c => c.Id).Contains(m.ConversationId))
            .Where(m => from == null || m.CreatedAt >= from)
            .Where(m => before == null || m.CreatedAt < before)
            .OrderBy(m => m.Sequence)
            .Select(m => new { m.ConversationId, m.Sequence, m.Role, m.Content, m.CreatedAt })
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return "No past conversation matches.";
        }

        var titles = await conversations.ToDictionaryAsync(c => c.Id, c => c.Title, cancellationToken);
        var terms = Words(query).Distinct().ToList();
        var docs = messages.Select(m => Words(m.Content)).ToList();
        var average = docs.Average(d => d.Count);
        var frequency = terms.ToDictionary(t => t, t => docs.Count(d => d.Contains(t)));
        var hits = docs
            .Select((d, i) => (Index: i, Score: terms.Sum(t => Bm25(d, t, frequency[t], docs.Count, average))))
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .Take(MaxHits)
            .ToList();
        if (hits.Count == 0)
        {
            return "No past conversation matches.";
        }

        var answer = new StringBuilder();
        foreach (var hit in hits)
        {
            var m = messages[hit.Index];
            answer.Append(CultureInfo.InvariantCulture, $"--- {m.CreatedAt:yyyy-MM-dd HH:mm} UTC · \"{titles.GetValueOrDefault(m.ConversationId)}\" · {(m.Role == ConversationMessage.AssistantRole ? "you" : "the person")}:\n");
            answer.Append(Excerpt(m.Content, terms)).Append('\n');
            var next = messages.FirstOrDefault(n => n.ConversationId == m.ConversationId && n.Sequence > m.Sequence);
            if (next is not null)
            {
                answer.Append(CultureInfo.InvariantCulture, $"(then {(next.Role == ConversationMessage.AssistantRole ? "you" : "the person")}: {Cut(next.Content, 300)})\n");
            }
        }

        return answer.ToString();
    }

    /// <summary>The start of a long message (where its context often is) and the part around the best-matching word.</summary>
    private static string Excerpt(string content, IReadOnlyList<string> terms)
    {
        if (content.Length <= 2 * Window)
        {
            return content;
        }

        var lower = content.ToLowerInvariant();
        var at = terms.Select(t => lower.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, at - Window / 2);
        var head = Cut(content, 200);
        return start <= 200 ? Cut(content, 2 * Window) : $"{head}\n[…]\n{content.Substring(start, Math.Min(Window, content.Length - start))}…";
    }

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

    private static DateTimeOffset? Day(string? day) =>
        DateTimeOffset.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private static string Cut(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private static List<string> Words(string text) => WordPattern().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
