namespace Filum.Engine;

/// <summary>What consolidation reads and leaves (spec 030): pending events, passes, proposals.</summary>
public sealed partial class MemoryService
{
    /// <summary>What was said, answered or imported after the last pass, oldest first, at most <paramref name="max"/>.</summary>
    public async Task<IReadOnlyList<MemoryEvent>> PendingAsync(Guid userId, int max, CancellationToken cancellationToken)
    {
        var passes = await Store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Derived]), cancellationToken);
        var through = passes
            .Where(e => e.Source == MemoryEventSource.Consolidation && e.Text == Consolidation.PassText)
            .SelectMany(e => e.Sources)
            .DefaultIfEmpty(0)
            .Max();
        var pending = await Store.EventsAsync(userId, new EventQuery(AfterId: through, Kinds: MemoryEventKind.Spoken), cancellationToken);
        return pending.Take(max).ToList();
    }

    /// <summary>Closes a pass: the events it read are its sources, the revisions it made are listed.</summary>
    public Task<MemoryEvent> CloseConsolidationAsync(Guid userId, IReadOnlyList<long> read, IReadOnlyList<long> revisions, CancellationToken cancellationToken) =>
        RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Consolidation, Consolidation.PassText, Sources: read, Revisions: revisions), cancellationToken);

    /// <summary>A structural change consolidation suggests: nothing is created until the person says yes.</summary>
    public Task<MemoryEvent> ProposeAsync(Guid userId, string text, IReadOnlyList<long> sources, CancellationToken cancellationToken) =>
        RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Proposed, MemoryEventSource.Consolidation, text, Sources: sources), cancellationToken);

    /// <summary>The proposals the person has not answered yet, newest first, from the last <see cref="Consolidation.ProposalLifetime"/>.</summary>
    public async Task<IReadOnlyList<MemoryEvent>> OpenProposalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var proposals = await Store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Proposed], From: DateTimeOffset.UtcNow - Consolidation.ProposalLifetime), cancellationToken);
        if (proposals.Count == 0)
        {
            return [];
        }

        var answered = (await Store.EventsAsync(userId, new EventQuery(AfterId: proposals[0].Id - 1, Kinds: [MemoryEventKind.Corrected]), cancellationToken))
            .SelectMany(e => e.Sources)
            .ToHashSet();
        return proposals.Where(p => !answered.Contains(p.Id)).Reverse().ToList();
    }

    /// <summary>Records the person's answer to a proposal; refused when it is not an open proposal of theirs.</summary>
    public async Task<MemoryOutcome<MemoryEvent>> AnswerProposalAsync(Guid userId, long proposalId, bool accepted, string source, CancellationToken cancellationToken)
    {
        var open = await OpenProposalsAsync(userId, cancellationToken);
        var proposal = open.FirstOrDefault(p => p.Id == proposalId);
        if (proposal is null)
        {
            return MemoryOutcome<MemoryEvent>.Missing($"There is no open proposal {proposalId}.");
        }

        var answer = await RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Corrected, source, accepted ? "Accepted the proposal" : "Declined the proposal", Sources: [proposal.Id]), cancellationToken);
        return MemoryOutcome<MemoryEvent>.Ok(answer);
    }

    /// <summary>Who made the latest change of the live file at <paramref name="path"/>, or null when there is none.</summary>
    public async Task<string?> LastAuthorAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        var file = await Store.FindLiveAsync(userId, path, cancellationToken);
        return file is null ? null : (await Store.GetRevisionAsync(userId, file.LatestRevisionId, cancellationToken))?.Author;
    }
}
