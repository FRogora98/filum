using Filum.Engine.DependencyInjection;

namespace Filum.Engine.Storage.InMemory;

/// <summary>Holds an engine's memory in the process.</summary>
public static class InMemoryEngineBuilderExtensions
{
    /// <summary>Holds the memory in a new <see cref="InMemoryMemoryStore"/>: nothing is persisted.</summary>
    public static FilumEngineBuilder UseInMemoryStore(this FilumEngineBuilder engine) => engine.UseStore<InMemoryMemoryStore>();
}
