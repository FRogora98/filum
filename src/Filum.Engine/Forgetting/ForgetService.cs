using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Collections;
using Filum.Engine.Events;
using Filum.Engine.Facts;
using Filum.Engine.Memory.Files;
using Filum.Engine.Memory;
using Microsoft.Extensions.Logging;

namespace Filum.Engine.Forgetting;

// The right to be forgotten: events and files go for good, following their sources.
internal sealed class ForgetService(IMemoryStore store, MemoryWriter writer, EventService events, ILogger<ForgetService> logger)
{
    public async Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        var files = new List<Guid>();
        foreach (var path in paths)
        {
            if (await store.FindLiveAsync(userId, path, cancellationToken) is { } file)
            {
                files.Add(file.Id);
            }
        }

        await store.ForgetAsync(userId, eventIds, files, cancellationToken);
        logger.Forgot(eventIds.Count, files.Count, userId);
    }

    public async Task<int> ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var said = (await store.EventsAsync(userId, new EventQuery(ConversationId: conversationId), cancellationToken))
            .Where(e => e.Kind is MemoryEventKind.Said or MemoryEventKind.Answered or MemoryEventKind.Told || (e.Kind == MemoryEventKind.Derived && e.Revisions.Count == 0))
            .Select(e => e.Id)
            .ToList();
        if (said.Count > 0)
        {
            await store.ForgetAsync(userId, said, [], cancellationToken);
        }

        return said.Count;
    }

    public async Task<ForgetResult> ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var ofConversation = await store.EventsAsync(userId, new EventQuery(ConversationId: conversationId), cancellationToken);
        if (ofConversation.Count == 0)
        {
            return new ForgetResult(0, 0, 0);
        }

        var forgotten = ofConversation.Select(e => e.Id).ToHashSet();
        var passRevisions = await AddPassesOnlyFromAsync(userId, forgotten, cancellationToken);
        var files = await FilesOnlyFromAsync(userId, conversationId, ofConversation, passRevisions, cancellationToken);
        var rows = await RemoveFactsAsync(userId, conversationId, forgotten, files, cancellationToken);

        await store.ForgetAsync(userId, forgotten, files, cancellationToken);
        await events.RecordAsync(userId, new NewMemoryEvent(MemoryEventKind.Corrected, MemoryEventSource.App,
            $"Forgot a conversation: {forgotten.Count} events, {files.Count} files, {rows} facts"), cancellationToken);
        logger.ConversationForgotten(userId, conversationId, forgotten.Count, files.Count, rows);
        return new ForgetResult(forgotten.Count, files.Count, rows);
    }

    // Passes and proposals that read only forgotten events are forgotten too; returns the revisions those passes made.
    private async Task<HashSet<long>> AddPassesOnlyFromAsync(Guid userId, HashSet<long> forgotten, CancellationToken cancellationToken)
    {
        var derived = await store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Derived, MemoryEventKind.Proposed]), cancellationToken);
        var onlyFromIt = derived.Where(e => e.ConversationId is null && OnlyFrom(e.Sources, forgotten)).ToList();
        var passRevisions = onlyFromIt.SelectMany(e => e.Revisions).ToHashSet();
        forgotten.UnionWith(onlyFromIt.Select(e => e.Id));
        return passRevisions;
    }

    // Files every change of which came from the conversation's turns or passes.
    private async Task<List<Guid>> FilesOnlyFromAsync(
        Guid userId, Guid conversationId, IReadOnlyList<MemoryEvent> ofConversation, HashSet<long> passRevisions, CancellationToken cancellationToken)
    {
        var touched = ofConversation.SelectMany(e => e.Revisions).Concat(passRevisions).Distinct().ToList();
        var files = new List<Guid>();
        foreach (var fileId in (await store.RevisionsAsync(userId, touched, cancellationToken)).Select(r => r.FileId).Distinct())
        {
            var revisions = await store.RevisionsOfFileAsync(userId, fileId, cancellationToken);
            if (revisions.All(r => r.ConversationId == conversationId || passRevisions.Contains(r.Id)))
            {
                files.Add(fileId);
            }
        }

        return files;
    }

    // Facts sourced only on what is forgotten leave the facts collection, as a change the person made.
    private async Task<int> RemoveFactsAsync(Guid userId, Guid conversationId, HashSet<long> forgotten, List<Guid> files, CancellationToken cancellationToken)
    {
        var facts = await store.FindLiveAsync(userId, FactTable.Path, cancellationToken);
        if (facts is null || files.Contains(facts.Id) || FactTable.Read(Csv.Check(facts.Content)) is not { } all || !all.Any(f => OnlyFrom(f.Sources, forgotten) || Quotes(f, forgotten)))
        {
            return 0;
        }

        var rows = 0;
        // The rows are filtered from the file as the change reads it, so a fact recorded meanwhile stays.
        var removed = await writer.ChangeAsync(userId, MemoryActor.Person, FactTable.Path, MemoryOperation.Edit, (file, _) =>
        {
            if (file is null || FactTable.Read(Csv.Check(file.Content)) is not { } current)
            {
                return null;
            }

            var table = Csv.Check(file.Content);
            var quote = FactTable.Columns(table)[^1];
            var keep = table.Rows.Select((r, i) => (Row: r, Fact: current[i])).Where(x => !OnlyFrom(x.Fact.Sources, forgotten))
                .Select(x => quote >= 0 && Quotes(x.Fact, forgotten) ? x.Row.Fields.Select((f, c) => c == quote ? string.Empty : f).ToList() : x.Row.Fields)
                .ToList();
            rows = table.Rows.Count - keep.Count;
            return new ChangeTarget(FactTable.Path, Csv.Write(table.Header, keep), file.Sensitivity, Deleted: false);
        }, cancellationToken);
        if (removed.IsRefused)
        {
            logger.ForgottenFactsKept(userId, conversationId);
            return 0;
        }

        return rows;
    }

    private static bool OnlyFrom(IReadOnlyList<long> sources, HashSet<long> forgotten) => sources.Count > 0 && sources.All(forgotten.Contains);

    // A fact keeps the words of its first source (spec 041): they go with that source.
    private static bool Quotes(Fact fact, HashSet<long> forgotten) => fact.Quote.Length > 0 && fact.Sources.Count > 0 && forgotten.Contains(fact.Sources[0]);
}
