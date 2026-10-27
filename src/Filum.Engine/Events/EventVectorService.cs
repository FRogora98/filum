using System.Collections.Concurrent;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Events;

internal sealed class EventVectorService(IMemoryStore store, IEmbedder embedder, IOptions<MemoryOptions> options) : IEventVectors
{
    private const int BatchSize = 32;

    // The vectors of texts that are not events (facts, spec 042), by text, until there are this many.
    private const int MaxKeptTexts = 20_000;

    private readonly ConcurrentDictionary<string, float[]> _texts = new(StringComparer.Ordinal);

    private MemoryOptions Limits => options.Value;

    public async Task<int> MakeAsync(Guid userId, int max, CancellationToken cancellationToken)
    {
        var events = await store.EventsAsync(userId, new EventQuery(Kinds: MemoryEventKind.Spoken), cancellationToken);
        var before = (await store.VectorsAsync(userId, cancellationToken)).Count;
        var after = (await VectorsOfAsync(userId, events, max, cancellationToken)).Count;
        return after - before;
    }

    public async Task<IReadOnlyList<MemoryEventHit>> RankAsync(Guid userId, IReadOnlyList<MemoryEvent> events, string query, int max, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return EventSearch.Rank(events, query, max);
        }

        var kept = await VectorsOfAsync(userId, events, Limits.VectorsPerSearch, cancellationToken);
        var asked = embedder.Embed([query], query: true)[0];
        return EventSearch.Fuse(events, query, kept, asked, max);
    }

    public IReadOnlyList<float>? Similarity(string query, IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var missing = texts.Where(t => !_texts.ContainsKey(t)).Distinct(StringComparer.Ordinal).ToList();
        if (_texts.Count + missing.Count > MaxKeptTexts)
        {
            _texts.Clear();
        }

        foreach (var batch in missing.Chunk(BatchSize))
        {
            var vectors = embedder.Embed(batch, query: false);
            for (var i = 0; i < batch.Length; i++)
            {
                _texts[batch[i]] = vectors[i];
            }
        }

        var asked = embedder.Embed([query], query: true)[0];
        return [.. texts.Select(t => _texts.TryGetValue(t, out var v) ? MemoryVectors.Dot(v, asked) : 0f)];
    }

    // Missing vectors are made first, newest first, up to a bound.
    public async Task<IReadOnlyDictionary<long, float[]>> VectorsOfAsync(Guid userId, IReadOnlyList<MemoryEvent> events, int max, CancellationToken cancellationToken)
    {
        await SameModelAsync(userId, cancellationToken);
        var kept = await store.VectorsAsync(userId, cancellationToken);
        // Texts of a similar length share a batch: the model pads every text of a batch to the longest.
        var missing = events.Where(e => !kept.ContainsKey(e.Id)).OrderByDescending(e => e.Id).Take(max).OrderBy(e => e.Text.Length).ToList();
        if (missing.Count == 0)
        {
            return kept;
        }

        var made = new List<(long, float[])>();
        foreach (var batch in missing.Chunk(BatchSize))
        {
            var vectors = embedder.Embed(batch.Select(e => e.Text).ToList(), query: false);
            made.AddRange(batch.Select((e, i) => (e.Id, vectors[i])));
        }

        await store.SaveVectorsAsync(userId, made, cancellationToken);
        var all = new Dictionary<long, float[]>(kept);
        foreach (var (id, vector) in made)
        {
            all[id] = vector;
        }

        return all;
    }

    // Vectors of another model are dropped (spec 039); a store that does not know its model's id has vectors of the only
    // model used before ids were kept, which stay.
    private async Task SameModelAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (embedder.ModelId is not { } id || store is not IMemoryVectorModels models)
        {
            return;
        }

        var stored = await models.VectorModelAsync(userId, cancellationToken);
        if (stored != id)
        {
            await models.SetVectorModelAsync(userId, id, dropVectors: stored is not null, cancellationToken);
        }
    }
}
