using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Collections.Compute;
using Filum.Engine.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tools;

internal sealed class MemoryToolsFactory(IMemoryService memory, IOptions<MemoryOptions> options, ICodeSandbox sandbox) : IMemoryToolsFactory
{
    public MemoryTools Create(Guid userId, MemoryActor actor, IReadOnlyList<long>? sources = null, bool consolidation = false) =>
        new(memory, options.Value, sandbox, userId, actor, sources, consolidation);

    // Only described, never called, so built for no one.
    public IReadOnlyList<AIFunction> Catalog() =>
        Create(Guid.Empty, MemoryActor.Platform).Tools.Cast<AIFunction>().ToList();
}
