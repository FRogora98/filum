using Filum.Engine.Abstractions.Memory;
using Filum.Mcp.Session;
using Microsoft.Extensions.AI;

namespace Filum.Mcp.Tools;

// Each tool runs for the folder's owner as the host's agent, in this session.
internal sealed class EngineToolCatalog(McpEngines engines, OpenedMemory memory, ServerSession session)
{
    public IReadOnlyList<AIFunction> Functions() =>
        engines.Current.Tools.Catalog()
            .Select(template => (AIFunction)new PerCallFunction(template, () =>
                engines.Current.Tools.Create(memory.Owner, MemoryActor.Agent(session.Id, Guid.NewGuid()))))
            .ToList();
}
