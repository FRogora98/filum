using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// Consolidation passes over what was said, and the proposals they make to the person. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryConsolidation
{
    /// <summary>What was said, answered or imported after the last pass, oldest first, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<MemoryEvent>> PendingAsync(Guid userId, int max, CancellationToken cancellationToken);

    /// <summary>
    /// The pending events a pass should read: with a model and a gate, those that say something about the person's life;
    /// otherwise all of them. The pass still closes every pending event, read or not.
    /// </summary>
    Task<IReadOnlyList<MemoryEvent>> GateAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken);

    /// <summary>Closes a pass: the events it read are its sources, the revisions it made are listed.</summary>
    Task<MemoryEvent> CloseConsolidationAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken);

    /// <summary>A structural change consolidation suggests: nothing is created until the person says yes.</summary>
    Task<MemoryEvent> ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken);

    /// <summary>The proposals the person has not answered yet, newest first, from the last two weeks.</summary>
    Task<IReadOnlyList<MemoryEvent>> OpenProposalsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records the person's answer to a proposal; refused when it is not an open proposal of theirs.</summary>
    Task<MemoryOutcome<MemoryEvent>> AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken);

}
