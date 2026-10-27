using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Events;

namespace Filum.Engine.Consolidation;

// An event without a vector is kept; a discarded one stays in the log.
internal sealed class GatedPendingEventFilter(EventVectorService vectors, IEventGate gate) : IPendingEventFilter
{
    public async Task<IReadOnlyList<MemoryEvent>> FilterAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken)
    {
        if (pending.Count == 0)
        {
            return pending;
        }

        var kept = await vectors.VectorsOfAsync(userId, pending, int.MaxValue, cancellationToken);
        return pending.Where(e => !kept.TryGetValue(e.Id, out var vector) || gate.Keep(vector)).ToList();
    }
}
