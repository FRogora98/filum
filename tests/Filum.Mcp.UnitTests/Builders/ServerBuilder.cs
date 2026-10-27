using Filum.Engine.Abstractions.Options;
using Filum.Engine.Storage.InMemory;
using Filum.Mcp.Configuration;
using Filum.Mcp.Search;
using Filum.Mcp.Session;
using Filum.Mcp.Tools;
using Filum.Mcp.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.UnitTests.Builders;

internal sealed class ServerBuilder : IDisposable
{
    private readonly FakeLifetime _lifetime = new();

    public ServerBuilder()
    {
        Memory.Open(new InMemoryMemoryStore(), Guid.NewGuid(), pack: null);
        Engines = new McpEngines(Memory, Model, Reranker, Options.Create(new McpOptions()), Options.Create(new MemoryOptions()), NullLoggerFactory.Instance);
        Vectors = new VectorMaker(Engines, Memory, Model, _lifetime, NullLogger<VectorMaker>.Instance);
    }

    public OpenedMemory Memory { get; } = new();

    public ModelSlot Model { get; } = new();

    public RerankerSlot Reranker { get; } = new();

    public ServerSession Session { get; } = ServerSession.New();

    public McpEngines Engines { get; }

    public VectorMaker Vectors { get; }

    public LogToolStrategy LogToolStrategy() => new(Engines, Memory, Session);

    public ConsolidateToolStrategy ConsolidateToolStrategy() => new(Engines, Memory, Vectors);

    public ImportToolStrategy ImportToolStrategy() => new(Engines, Memory, Vectors);

    public EngineToolCatalog Catalog() => new(Engines, Memory, Session);

    public void Dispose()
    {
        Engines.Dispose();
        Memory.Dispose();
        Model.Dispose();
        _lifetime.Dispose();
    }
}
