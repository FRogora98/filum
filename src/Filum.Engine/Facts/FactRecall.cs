using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Events;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Facts;

// The facts that may answer a question (spec 042): by its words and, with the embedding model, by its meaning, so a fact
// is reached without its subject's name ("which city did she move to" reaches "Anna · city: Lyon").
internal sealed class FactRecall(FactService facts, IEventVectors vectors)
{
    public const int Max = 5;

    // Below this cosine, a fact found only by meaning is not shown: e5's cosines of unrelated short texts sit around 0.75.
    private const float MeaningFloor = 0.80f;

    // Reciprocal rank fusion's usual constant.
    private const double FusionK = 60;

    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> MatchAsync(Guid userId, string? query, CancellationToken cancellationToken)
    {
        if (!FileRefusals.IsValidQuery(query))
        {
            return MemoryOutcome<IReadOnlyList<Fact>>.Refused(FileRefusals.QueryLength);
        }

        var current = await facts.CurrentAsync(userId, null, null, cancellationToken);
        if (current.IsRefused)
        {
            return current;
        }

        // A value that starts later answers a question about it too (spec 055).
        IReadOnlyList<Fact> candidates = [.. current.Value, .. (await facts.PlannedAsync(userId, null, cancellationToken)).Value ?? []];
        var all = candidates.Where(f => !FactTable.Same(f.Attribute, FactTable.Alias)).ToList();
        var aliases = candidates.Where(f => FactTable.Same(f.Attribute, FactTable.Alias))
            .ToLookup(f => f.Subject.Trim(), f => f.Value, StringComparer.OrdinalIgnoreCase);
        var texts = all.Select(f => string.Join(' ', [f.Subject, .. aliases[f.Subject.Trim()], f.Attribute, f.Value])).ToList();
        var score = new double[all.Count];
        Fuse(score, EventSearch.Scores(texts, query!).Select((s, i) => (Score: s, Index: i)).Where(x => x.Score > 0));
        if (vectors.Similarity(query!, texts) is { } meaning)
        {
            Fuse(score, meaning.Select((s, i) => (Score: (double)s, Index: i)).Where(x => x.Score >= MeaningFloor));
        }

        return MemoryOutcome<IReadOnlyList<Fact>>.Ok([.. score
            .Select((s, i) => (Score: s, Index: i))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Take(Max)
            .Select(x => all[x.Index])]);
    }

    private static void Fuse(double[] score, IEnumerable<(double Score, int Index)> ranking)
    {
        var rank = 0;
        foreach (var (_, index) in ranking.OrderByDescending(x => x.Score))
        {
            score[index] += 1 / (FusionK + ++rank);
        }
    }
}
