using Filum.Engine.Abstractions.Memory;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

/// <summary>Builds the agent's tools for one turn, and describes the catalog every host offers.</summary>
public interface IMemoryToolsFactory
{
    /// <summary>
    /// The tools of one turn for <paramref name="userId"/>, acting as <paramref name="actor"/>. <paramref name="sources"/>
    /// are the events the turn answers; with <paramref name="consolidation"/>, the restricted tools of a consolidation pass.
    /// </summary>
    MemoryTools Create(Guid userId, MemoryActor actor, IReadOnlyList<long>? sources = null, bool consolidation = false);

    /// <summary>
    /// The catalog of the engine's tools, as any host sees them with the configured limits: names, descriptions and
    /// parameter schemas. The one source of truth of the tool surface.
    /// </summary>
    IReadOnlyList<AIFunction> Catalog();
}
