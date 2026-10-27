using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;
using Filum.Engine.Packs;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Fakes;

namespace Filum.Engine.Testing.Builders;

/// <summary>An <see cref="IMemoryService"/> as the tests build it: no log, and the default limits unless given.</summary>
public static class MemoryServices
{
    /// <summary>A memory on a new in-memory store.</summary>
    public static IMemoryService InMemory(MemoryOptions? options = null) => On(new InMemoryMemoryStore(), options);

    /// <summary>A memory on <paramref name="store"/>; <paramref name="beforeSave"/> runs right before each save.</summary>
    public static IMemoryService On(
        IMemoryStore store,
        MemoryOptions? options = null,
        Func<CancellationToken, Task>? beforeSave = null,
        Pack? pack = null,
        IEmbedder? embedder = null,
        IEventGate? gate = null,
        IReranker? reranker = null) =>
        FilumEngine.Create(beforeSave is null ? store : BeforeSaveStore.Wrap(store, beforeSave), options, engine =>
        {
            if (pack is not null)
            {
                engine.UsePack(pack);
            }

            if (embedder is not null)
            {
                engine.UseEmbedder(embedder);
            }

            if (gate is not null)
            {
                engine.UseEventGate(gate);
            }

            if (reranker is not null)
            {
                engine.UseReranker(reranker);
            }
        }).Memory;
}
