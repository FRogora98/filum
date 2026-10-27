using Filum.Embeddings;
using Filum.Engine.Memory.Contracts;

namespace Filum.Evals.LongMemEval;

// A question's whole history written into its own memory folder, as filum-mcp's hook would have kept it (spec 043): each
// history session a conversation, each turn an event dated by its session, the person's turns "said" and the
// assistant's "answered"; then the search vectors, as filum-mcp's background pass would make them before the question.
// The sessions keep the file's order, as the published LongMemEval rounds wrote them.
internal static class LongMemEvalPreload
{
    public static IReadOnlyList<NewMemoryEvent> Events(LongMemEvalInstance instance)
    {
        var events = new List<NewMemoryEvent>();
        for (var s = 0; s < instance.HaystackSessions.Count; s++)
        {
            var conversation = Guid.NewGuid();
            var when = new DateTimeOffset(LongMemEvalInstance.When(s < instance.HaystackDates.Count ? instance.HaystackDates[s] : string.Empty), TimeSpan.Zero);
            var turn = 0;
            foreach (var t in instance.HaystackSessions[s].Where(t => t.Content.Length > 0))
            {
                // A second apart, so the turns keep their order.
                events.Add(new NewMemoryEvent(t.ByAssistant ? MemoryEventKind.Answered : MemoryEventKind.Said, MemoryEventSource.Chat, t.Content, when.AddSeconds(turn++), conversation));
            }
        }

        return events;
    }

    // The folder is written again from nothing; without a model, no vectors (search by words only).
    public static async Task<int> WriteAsync(LongMemEvalInstance instance, string home, E5Embedder? embedder, CancellationToken cancellationToken)
    {
        if (Directory.Exists(home))
        {
            Directory.Delete(home, recursive: true);
        }

        var events = Events(instance);
        using var store = new LocalFolderStore(home);
        await store.AppendEventsAsync(store.Owner, events, DateTimeOffset.UtcNow, cancellationToken);
        if (embedder is not null)
        {
            using var engine = FilumEngine.Create(store, configure: e => e.UseEmbedder(embedder));
            IMemoryEvents memory = engine.Memory;
            await memory.MakeVectorsAsync(store.Owner, int.MaxValue, cancellationToken);
        }

        return events.Count;
    }
}
