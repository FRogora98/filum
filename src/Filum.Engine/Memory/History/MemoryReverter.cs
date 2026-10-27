using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory.Files;
using Microsoft.Extensions.Logging;

namespace Filum.Engine.Memory.History;

// Changes are taken back as new revisions, through the writer's single save path.
internal sealed class MemoryReverter(IMemoryRevisionStore store, MemoryWriter writer, ILogger<MemoryReverter> logger)
{
    public Task<MemoryOutcome<MemoryChange>> UndoAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken) =>
        TakeBackAsync(userId, actor, revisionId, new HashSet<long> { revisionId }, MemoryOperation.Undo, cancellationToken);

    public async Task RollbackAsync(Guid userId, IReadOnlyList<long> revisionIds, CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return;
        }

        var turn = (await store.RevisionsAsync(userId, revisionIds, cancellationToken)).Select(r => (r.FileId, r.Id)).ToList();

        foreach (var file in turn.GroupBy(r => r.FileId))
        {
            var ids = file.Select(r => r.Id).ToHashSet();
            var outcome = await TakeBackAsync(userId, MemoryActor.Platform, ids.Min(), ids, MemoryOperation.Rollback, cancellationToken);
            if (outcome.IsRefused)
            {
                logger.RollbackKept(userId, string.Join(",", ids));
            }
        }
    }

    public async Task<MemoryOutcome<MemoryChange>> RestoreAsync(Guid userId, MemoryActor actor, long revisionId, CancellationToken cancellationToken)
    {
        var revision = await store.GetRevisionAsync(userId, revisionId, cancellationToken);
        if (revision is null)
        {
            return MemoryOutcome<MemoryChange>.Missing(FileRefusals.NoChange(revisionId));
        }

        if (revision.Deleted)
        {
            return MemoryOutcome<MemoryChange>.Refused("That version is the deletion of the file; pick an earlier one.");
        }

        return await writer.ChangeFileAsync(userId, actor, revision.FileId, MemoryOperation.Restore, null, revision.Path, (_, pathTaken) =>
            pathTaken
                ? ChangeTarget.Refuse(AnotherFileUses(revision.Path))
                : new ChangeTarget(revision.Path, revision.Content, revision.Sensitivity, Deleted: false), cancellationToken);
    }

    // Only while the file's latest revision is one of ownRevisions: otherwise someone changed it since, and that would
    // be lost.
    private async Task<MemoryOutcome<MemoryChange>> TakeBackAsync(Guid userId, MemoryActor actor, long revisionId, IReadOnlySet<long> ownRevisions, string operation, CancellationToken cancellationToken)
    {
        var revision = await store.GetRevisionAsync(userId, revisionId, cancellationToken);
        if (revision is null)
        {
            return MemoryOutcome<MemoryChange>.Missing(FileRefusals.NoChange(revisionId));
        }

        var ofFile = await store.RevisionsOfFileAsync(userId, revision.FileId, cancellationToken);
        var latest = ofFile[^1];
        var previous = ofFile.LastOrDefault(r => r.Id < revision.Id);

        if (!ownRevisions.Contains(latest.Id))
        {
            return MemoryOutcome<MemoryChange>.Refused(
                $"{latest.Path} changed again after that ({Describe(latest)} at {latest.CreatedAt:yyyy-MM-dd HH:mm} UTC, change {latest.Id}); undo that first.");
        }

        return await writer.ChangeFileAsync(userId, actor, revision.FileId, operation, revision.Id, previous?.Path, (file, previousPathTaken) =>
            previous is null
                ? new ChangeTarget(file.Path, file.Content, file.Sensitivity, Deleted: true)
                : !previous.Deleted && previousPathTaken
                    ? ChangeTarget.Refuse(AnotherFileUses(previous.Path))
                    : new ChangeTarget(previous.Path, previous.Content, previous.Sensitivity, previous.Deleted), cancellationToken);
    }

    private static string AnotherFileUses(string path) => $"Another file now uses {path}.";

    private static string Describe(StoredRevision revision) =>
        $"{revision.Operation} by {(revision.Author == MemoryAuthor.Agent ? "Filum" : revision.Author == MemoryAuthor.Person ? "you" : "the platform")}";
}
