using System.Diagnostics;
using System.Globalization;
using System.Text;
using Filum.Engine.Memory.Contracts;

namespace Filum.Evals.LongMemEval.Analysis;

// No model, no cost: each question's history in a fresh memory, searched with events_search's ranking; a hit counts
// when it belongs to a session holding the answer. Abstention questions have none and are left out.
internal static class LongMemEvalRetrieval
{
    // Recall is measured at the first FewHits and at all of them.
    public const int Hits = 10;

    public const int FewHits = 5;

    // As the tool takes it.
    private const int MaxQueryLength = 200;

    private const string NoRows = "—";

    public static async Task<IReadOnlyList<LongMemEvalRetrievalRow>> RunAsync(IReadOnlyList<LongMemEvalInstance> instances, Func<IMemoryStore> stores, TextWriter progress, TimeProvider time, CancellationToken cancellationToken, IEmbedder? embedder = null)
    {
        var rows = new List<LongMemEvalRetrievalRow>();
        foreach (var instance in instances)
        {
            if (instance.IsAbstention || instance is not { AnswerSessionIds: { Count: > 0 } answers, HaystackSessionIds: { } sessionIds })
            {
                continue;
            }

            var row = await MeasureAsync(instance, sessionIds, answers, stores(), embedder, time, cancellationToken);
            rows.Add(row);
            progress.WriteLine($"[{rows.Count}] {row.QuestionId} ({row.Ability}): @5 {(row.At5 ? "yes" : "no")}, @10 {(row.At10 ? "yes" : "no")} · {row.Events} events · {row.Seconds:0.0}s");
        }

        return rows;
    }

    public static string Markdown(IReadOnlyList<LongMemEvalRetrievalRow> rows, string description, TimeProvider time)
    {
        var text = new StringBuilder($"# LongMemEval retrieval — {time.GetUtcNow():yyyy-MM-dd HH:mm}\n\n{description}\n\n");
        text.Append("| Ability | Questions | Recall@5 (any) | Recall@10 (any) | Answer sessions found @10 |\n|---|---|---|---|---|\n");
        foreach (var ability in LongMemEvalAbilities.All.Where(a => a != LongMemEvalAbilities.Abstention))
        {
            var of = rows.Where(r => r.Ability == ability).ToList();
            text.Append(CultureInfo.InvariantCulture, $"| {ability} | {of.Count} | {Rate(of, r => r.At5)} | {Rate(of, r => r.At10)} | {Share(of)} |\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"| **all** | {rows.Count} | **{Rate(rows, r => r.At5)}** | **{Rate(rows, r => r.At10)}** | **{Share(rows)}** |\n");
        return text.ToString();
    }

    private static async Task<LongMemEvalRetrievalRow> MeasureAsync(
        LongMemEvalInstance instance, IReadOnlyList<string> sessionIds, IReadOnlyList<string> answerSessionIds, IMemoryStore store, IEmbedder? embedder, TimeProvider time, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        using var engine = FilumEngine.Create(store, new MemoryOptions { EventsSearchMax = Hits }, e => { if (embedder is not null) { e.UseEmbedder(embedder); } });
        IMemoryEvents memory = engine.Memory;
        var person = Guid.NewGuid();
        var sessionOf = new Dictionary<Guid, string>();
        var events = Events(instance, sessionIds, sessionOf);
        await store.AppendEventsAsync(person, events, time.GetUtcNow(), cancellationToken);
        // As a background pass would have, before the question.
        await memory.MakeVectorsAsync(person, int.MaxValue, cancellationToken);
        var hits = (await memory.SearchEventsAsync(person, Query(instance.Question), null, null, includePrivate: true, cancellationToken)).Value ?? [];
        var answers = answerSessionIds.ToHashSet(StringComparer.Ordinal);
        bool Within(int k) => hits.Take(k).Any(h => h.Event.ConversationId is { } c && answers.Contains(sessionOf[c]));
        var found = hits.Take(Hits).Select(h => h.Event.ConversationId is { } c ? sessionOf[c] : null).OfType<string>().Where(answers.Contains).Distinct().Count();
        return new LongMemEvalRetrievalRow(instance.QuestionId, instance.Ability, events.Count, Within(FewHits), Within(Hits), clock.Elapsed.TotalSeconds, answers.Count, found);
    }

    // sessionOf maps the events back to their sessions.
    private static List<NewMemoryEvent> Events(LongMemEvalInstance instance, IReadOnlyList<string> sessionIds, Dictionary<Guid, string> sessionOf)
    {
        var events = new List<NewMemoryEvent>();
        for (var s = 0; s < instance.HaystackSessions.Count; s++)
        {
            var session = Guid.NewGuid();
            var when = new DateTimeOffset(LongMemEvalInstance.When(s < instance.HaystackDates.Count ? instance.HaystackDates[s] : string.Empty), TimeSpan.Zero);
            sessionOf[session] = sessionIds[s];
            events.AddRange(instance.HaystackSessions[s].Select(t =>
                new NewMemoryEvent(t.ByAssistant ? MemoryEventKind.Answered : MemoryEventKind.Said, MemoryEventSource.Chat, t.Content, when, session)));
        }

        return events;
    }

    private static string Query(string question) => question.Length <= MaxQueryLength ? question : question[..MaxQueryLength];

    private static string Rate(IReadOnlyList<LongMemEvalRetrievalRow> rows, Func<LongMemEvalRetrievalRow, bool> hit) =>
        rows.Count > 0 ? Percent(100.0 * rows.Count(hit) / rows.Count) : NoRows;

    private static string Share(IReadOnlyList<LongMemEvalRetrievalRow> rows) =>
        rows.Count > 0 ? Percent(100.0 * rows.Sum(x => x.FoundAt10) / rows.Sum(x => x.AnswerSessions)) : NoRows;

    private static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}
