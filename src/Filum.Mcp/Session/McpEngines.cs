using Filum.Embeddings;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.DependencyInjection;
using Filum.Engine.Packs;
using Filum.Mcp.Configuration;
using Filum.Mcp.Search;
using Filum.Mcp.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.Session;

// The search model arrives while the server runs: one engine searches by words until then, the other by meaning too.
internal sealed class McpEngines(OpenedMemory memory, ModelSlot model, RerankerSlot reranker, IOptions<McpOptions> settings, IOptions<MemoryOptions> limits, ILoggerFactory logging) : IDisposable
{
    private readonly Lock _creating = new();
    private FilumEngine? _byWords;
    private FilumEngine? _byMeaning;

    public FilumEngine Current
    {
        get
        {
            var embedder = model.Current;
            lock (_creating)
            {
                return embedder is null
                    ? _byWords ??= Create(null)
                    : _byMeaning ??= Create(embedder);
            }
        }
    }

    public void Dispose()
    {
        _byWords?.Dispose();
        _byMeaning?.Dispose();
    }

    private FilumEngine Create(IEmbedder? embedder) =>
        FilumEngine.Create(memory.Store, limits.Value, engine => Configure(engine, memory.Pack, embedder, settings.Value.Reranks ? reranker : null), logging);

    private static void Configure(FilumEngineBuilder engine, Pack? pack, IEmbedder? embedder, IReranker? reranker)
    {
        engine.Services.AddSingleton(McpClock.Time);
        if (pack is not null)
        {
            engine.UsePack(pack);
        }

        // With FILUM_RERANKER=on, a search's best matches are ordered again once the reranker has loaded (spec 042).
        if (reranker is not null)
        {
            engine.UseReranker(reranker);
        }

        if (embedder is null)
        {
            return;
        }

        // With the model, a consolidation pass is also handed only what says something about the person.
        engine.UseEmbedder(embedder);
        if (KeepGate.Shipped() is { } gate)
        {
            engine.UseEventGate(gate);
        }
    }
}
