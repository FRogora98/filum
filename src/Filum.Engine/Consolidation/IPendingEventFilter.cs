using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Consolidation;

/// <summary>
/// Which pending events a consolidation pass reads. Chosen in composition: with an embedding model and an event gate,
/// <see cref="GatedPendingEventFilter"/>; otherwise <see cref="NullPendingEventFilter"/>, which keeps them all.
/// </summary>
internal interface IPendingEventFilter
{
    /// <summary>The events of <paramref name="pending"/> the pass should read.</summary>
    Task<IReadOnlyList<MemoryEvent>> FilterAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken);
}
