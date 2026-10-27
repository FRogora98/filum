using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Filum.Mcp.Search;

// In the background, after an import or a tidying.
internal sealed partial class VectorMaker
{
    private readonly ModelSlot _model;
    private readonly CoalescedWork _work;
    private readonly ILogger<VectorMaker> _logger;

    public VectorMaker(McpEngines engines, OpenedMemory memory, ModelSlot model, IHostApplicationLifetime lifetime, ILogger<VectorMaker> logger)
    {
        _model = model;
        _logger = logger;
        _work = new CoalescedWork(
            cancellationToken => MakeVectorsAsync(engines.Current.Memory, memory.Owner, cancellationToken),
            e => LogFailed(e.GetType().Name),
            lifetime.ApplicationStopping);
    }

    private static Task<int> MakeVectorsAsync(IMemoryEvents events, Guid owner, CancellationToken cancellationToken) =>
        events.MakeVectorsAsync(owner, int.MaxValue, cancellationToken);

    public Task MakeSoon() => _model.Current is null ? Task.CompletedTask : _work.Request();

    // Vectors are derived: the next search or pass makes the missing ones again.
    [LoggerMessage(EventId = 20, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": vectors were not made ({Error}); the next search or tidying makes them")]
    private partial void LogFailed(string error);
}
