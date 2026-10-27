using Filum.Engine.Abstractions.Options;
using Filum.Engine.Storage.InMemory;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Testing.Builders;

/// <summary>The engine's tool catalog as a host sees it, built on a new in-memory store.</summary>
public static class ToolCatalogs
{
    /// <summary>The catalog with <paramref name="options"/>, or the default limits.</summary>
    public static IReadOnlyList<AIFunction> Of(MemoryOptions? options = null)
    {
        using var engine = FilumEngine.Create(new InMemoryMemoryStore(), options);
        return engine.Tools.Catalog();
    }
}
