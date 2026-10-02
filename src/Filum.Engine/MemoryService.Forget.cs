using Microsoft.Extensions.Logging;

namespace Filum.Engine;

/// <summary>What forgetting a conversation removed.</summary>
public sealed record ForgetResult(int Events, int Files, int FactRows);

/// <summary>The person's right to be forgotten, following the sources (spec 030).</summary>
public sealed partial class MemoryService
{
    /// <summary>
    /// When a conversation is deleted, what was said in it leaves the log too: its messages, the instructions and the
    /// summaries made of it. What the memory made of it stays, as the conversation's deletion promises; forgetting it
    /// is <see cref="ForgetConversationAsync"/>.
    /// </summary>
    public async Task<int> ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var said = (await Store.EventsAsync(userId, new EventQuery(ConversationId: conversationId), cancellationToken))
            .Where(e => e.Kind is MemoryEventKind.Said or MemoryEventKind.Answered or MemoryEventKind.Told || (e.Kind == MemoryEventKind.Derived && e.Revisions.Count == 0))
            .Select(e => e.Id)
            .ToList();
        if (said.Count > 0)
        {
            await Store.ForgetAsync(userId, said, [], cancellationToken);
        }

        return said.Count;
    }

    /// <summary>
    /// Forgets a conversation for good: its events; the files every change of which came from it (from its turns, or
    /// from consolidation passes that read only its events); the facts sourced only on its events; the proposals made
    /// only from it. What also came from elsewhere stays. A <c>corrected</c> event records what was forgotten, without
    /// its content.
    /// </summary>
    public async Task<ForgetResult> ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var ofConversation = await Store.EventsAsync(userId, new EventQuery(ConversationId: conversationId), cancellationToken);
        if (ofConversation.Count == 0)
        {
            return new ForgetResult(0, 0, 0);
        }

        var forgotten = ofConversation.Select(e => e.Id).ToHashSet();
        bool OnlyFromIt(IReadOnlyList<long> sources) => sources.Count > 0 && sources.All(forgotten.Contains);

        // Passes and proposals that read only this conversation's events go with it.
        var derived = await Store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Derived, MemoryEventKind.Proposed]), cancellationToken);
        var onlyFromIt = derived.Where(e => e.ConversationId is null && OnlyFromIt(e.Sources)).ToList();
        var passRevisions = onlyFromIt.SelectMany(e => e.Revisions).ToHashSet();
        forgotten.UnionWith(onlyFromIt.Select(e => e.Id));

        var touched = ofConversation.SelectMany(e => e.Revisions).Concat(passRevisions).Distinct().ToList();
        var files = new List<Guid>();
        foreach (var fileId in (await Store.RevisionsAsync(userId, touched, cancellationToken)).Select(r => r.FileId).Distinct())
        {
            var revisions = await Store.RevisionsOfFileAsync(userId, fileId, cancellationToken);
            if (revisions.All(r => r.ConversationId == conversationId || passRevisions.Contains(r.Id)))
            {
                files.Add(fileId);
            }
        }

        // Facts sourced only on what is forgotten leave the facts collection, as a change the person made.
        var rows = 0;
        var facts = await Store.FindLiveAsync(userId, Facts.Path, cancellationToken);
        if (facts is not null && !files.Contains(facts.Id) && Facts.Read(Csv.Check(facts.Content)) is { } all && all.Any(f => OnlyFromIt(f.Sources)))
        {
            var table = Csv.Check(facts.Content);
            var keep = table.Rows.Where((_, i) => !OnlyFromIt(all[i].Sources)).Select(r => r.Fields).ToList();
            rows = table.Rows.Count - keep.Count;
            var removed = await ChangeAsync(userId, MemoryActor.Person, Facts.Path, MemoryOperation.Edit, cancellationToken, (file, _) =>
                file is null ? null : new Target(Facts.Path, Csv.Write(table.Header, keep), file.Sensitivity, Deleted: false));
            if (removed.IsRefused)
            {
                rows = 0;
                Logger.LogWarning("[ MemoryService ] The facts of a forgotten conversation of user {UserId} could not be removed: {Refusal}", userId, removed.Refusal);
            }
        }

        await Store.ForgetAsync(userId, forgotten, files, cancellationToken);
        await RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Corrected, MemoryEventSource.App,
            $"Forgot a conversation: {forgotten.Count} events, {files.Count} files, {rows} facts"), cancellationToken);
        Logger.LogInformation("[ MemoryService ] User {UserId} forgot conversation {ConversationId}: {Events} events, {Files} files, {Rows} facts", userId, conversationId, forgotten.Count, files.Count, rows);
        return new ForgetResult(forgotten.Count, files.Count, rows);
    }
}
