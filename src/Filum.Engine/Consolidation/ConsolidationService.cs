using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Events;

namespace Filum.Engine.Consolidation;

internal sealed class ConsolidationService(IMemoryEventStore store, EventService events, IPendingEventFilter filter, TimeProvider time)
{
    private const string Accepted = "Accepted the proposal";
    private const string Declined = "Declined the proposal";

    public async Task<IReadOnlyList<MemoryEvent>> PendingAsync(Guid userId, int max, CancellationToken cancellationToken)
    {
        var passes = await store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Derived]), cancellationToken);
        var through = passes
            .Where(e => e.Source == MemoryEventSource.Consolidation && e.Text == ConsolidationPrompt.PassText)
            .SelectMany(e => e.Sources)
            .DefaultIfEmpty(0)
            .Max();
        // Private events are never consolidated: nothing is inferred or extracted from them unless the person asks.
        var pending = await store.EventsAsync(userId, new EventQuery(AfterId: through, Kinds: MemoryEventKind.Spoken, IncludePrivate: false), cancellationToken);
        return pending.Take(max).ToList();
    }

    public Task<IReadOnlyList<MemoryEvent>> GateAsync(Guid userId, IReadOnlyList<MemoryEvent> pending, CancellationToken cancellationToken) =>
        filter.FilterAsync(userId, pending, cancellationToken);

    public Task<MemoryEvent> CloseAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken) =>
        events.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Consolidation, ConsolidationPrompt.PassText, Sources: read, Revisions: revisions), cancellationToken);

    public Task<MemoryEvent> ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        events.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Proposed, MemoryEventSource.Consolidation, text, Sources: sources), cancellationToken);

    public async Task<IReadOnlyList<MemoryEvent>> OpenProposalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var proposals = await store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Proposed], From: time.GetUtcNow() - ConsolidationPrompt.ProposalLifetime), cancellationToken);
        if (proposals.Count == 0)
        {
            return [];
        }

        var answered = (await store.EventsAsync(userId, new EventQuery(AfterId: proposals[0].Id - 1, Kinds: [MemoryEventKind.Corrected]), cancellationToken))
            .SelectMany(e => e.Sources)
            .ToHashSet();
        return proposals.Where(p => !answered.Contains(p.Id)).Reverse().ToList();
    }

    public async Task<MemoryOutcome<MemoryEvent>> AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken)
    {
        var open = await OpenProposalsAsync(userId, cancellationToken);
        var proposal = open.FirstOrDefault(p => p.Id == proposalId);
        if (proposal is null)
        {
            return MemoryOutcome<MemoryEvent>.Missing($"There is no open proposal {proposalId}.");
        }

        var answer = await events.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Corrected, source, accepted ? Accepted : Declined, Sources: [proposal.Id]), cancellationToken);
        return MemoryOutcome<MemoryEvent>.Ok(answer);
    }
}
