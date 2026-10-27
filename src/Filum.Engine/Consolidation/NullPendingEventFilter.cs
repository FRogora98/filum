using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Consolidation;

// Null object for a host with no gate: a pass reads every pending event.
internal sealed class NullPendingEventFilter : IPendingEventFilter
{
    public Task<IReadOnlyList<MemoryEvent>> FilterAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken) =>
        Task.FromResult(pending);
}
