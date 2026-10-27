using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Memory;
using Filum.Engine.Memory.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Events;

internal sealed partial class EventService(IMemoryEventStore store, IEventVectors vectors, TimeProvider time, IOptions<MemoryOptions> options, ILogger<EventService> logger, IReranker? reranker = null)
{
    // What a reranker reads of each match: its start, where what it is about usually is.
    private const int RerankChars = 1_000;

    // The text is kept as given, line breaks normalized and any secret removed (spec 048).
    public Task<MemoryEvent> RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken) =>
        store.AppendEventAsync(userId, next with { Text = SecretFilter.Redact(MemoryText.Normalize(next.Text)) }, time.GetUtcNow(), cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        store.EventsAsync(userId, query, cancellationToken);

    public async Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> SearchAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken)
    {
        if (!FileRefusals.IsValidQuery(query))
        {
            return MemoryOutcome<IReadOnlyList<MemoryEventHit>>.Refused(FileRefusals.QueryLength);
        }

        var events = await store.EventsAsync(userId, new EventQuery(Kinds: MemoryEventKind.Spoken, From: from, Before: before, IncludePrivate: includePrivate), cancellationToken);
        var max = options.Value.EventsSearchMax;
        var hits = await vectors.RankAsync(userId, events, query, reranker is null ? max : Math.Max(max, options.Value.RerankDepth), cancellationToken);
        return MemoryOutcome<IReadOnlyList<MemoryEventHit>>.Ok([.. Rerank(query, hits).Take(max)]);
    }

    // Every message that mentions an alternative of the query, and the best of a normal search for the whole of it (which
    // also finds a message by its meaning), oldest first (spec 057).
    public async Task<MemoryOutcome<IReadOnlyList<MemoryEvent>>> ListAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, int max, CancellationToken cancellationToken)
    {
        var searched = await SearchAsync(userId, query, from, before, includePrivate, cancellationToken);
        if (searched.IsRefused)
        {
            return MemoryOutcome<IReadOnlyList<MemoryEvent>>.Refused(searched.Refusal);
        }

        var best = searched.Value;

        var alternatives = EventSearch.Alternatives(query!);
        var events = await store.EventsAsync(userId, new EventQuery(Kinds: MemoryEventKind.Spoken, From: from, Before: before, IncludePrivate: includePrivate), cancellationToken);
        var listed = events.Where(e => alternatives.Any(a => EventSearch.Mentions(e.Text, a)))
            .Concat(best.Select(h => h.Event))
            .DistinctBy(e => e.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Take(Math.Max(0, max))
            .ToList();
        return MemoryOutcome<IReadOnlyList<MemoryEvent>>.Ok(listed);
    }

    // The reranker's order when it gives one; the search's own order when there is none, it cannot score now, or it fails.
    private IReadOnlyList<MemoryEventHit> Rerank(string query, IReadOnlyList<MemoryEventHit> hits)
    {
        if (reranker is null || hits.Count < 2)
        {
            return hits;
        }

        try
        {
            var scores = reranker.Score(query, [.. hits.Select(h => EventSearch.Cut(h.Event.Text, RerankChars))]);
            if (scores is null || scores.Count != hits.Count)
            {
                return hits;
            }

            return [.. hits.Select((h, i) => h with { Score = scores[i] }).OrderByDescending(h => h.Score).ThenByDescending(h => h.Event.Id)];
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogRerankFailed(logger, e.GetType().Name);
            return hits;
        }
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "The reranker failed ({Error}); the search keeps its own order")]
    private static partial void LogRerankFailed(ILogger logger, string error);
}
