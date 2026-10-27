using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Memory.Files;

// The single path of every change, undo and rollback included: one revision that wins only from the version it read.
internal sealed class MemoryWriter(
    IMemoryFileStore store,
    IOptions<MemoryOptions> options,
    ILogger<MemoryWriter> logger,
    TimeProvider time)
{
    private static readonly CompositeFormat ChangedAtTheSameTime = CompositeFormat.Parse("{0} was changed at the same time by another action; read it again and retry.");

    private readonly MemoryContentValidator validator = new(options);

    private MemoryOptions Limits => options.Value;

    public Task<MemoryOutcome<MemoryChange>> ChangeAsync(
        Guid userId, MemoryActor actor, string? path, string operation, Func<StoredFile?, bool, ChangeTarget?> decide, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, operation, (_, file, taken) => decide(file, taken), otherPath: null, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> ChangeAsync(
        Guid userId, MemoryActor actor, string? path, string operation, Func<string, StoredFile?, bool, ChangeTarget?> decide, CancellationToken cancellationToken) =>
        ChangeAsync(userId, actor, path, operation, decide, otherPath: null, cancellationToken);

    // decide gets the valid path, the file (null when there is none) and whether otherPath is taken by another live
    // file.
    public async Task<MemoryOutcome<MemoryChange>> ChangeAsync(
        Guid userId, MemoryActor actor, string? path, string operation, Func<string, StoredFile?, bool, ChangeTarget?> decide, string? otherPath, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<MemoryChange>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path, cancellationToken);
        var taken = otherPath is not null && await store.IsPathTakenAsync(userId, otherPath, null, cancellationToken);
        return await SaveAsync(userId, actor, operation, null, path, file, decide(path, file, taken), cancellationToken);
    }

    // By id, deleted or not: undo, rollback, restore.
    public async Task<MemoryOutcome<MemoryChange>> ChangeFileAsync(
        Guid userId, MemoryActor actor, Guid fileId, string operation, long? undoes, string? otherPath,
        Func<StoredFile, bool, ChangeTarget> decide, CancellationToken cancellationToken)
    {
        var file = await store.FindAsync(userId, fileId, cancellationToken)
            ?? throw new InvalidOperationException($"The file {fileId} of a revision is missing from the store.");
        var taken = otherPath is not null && await store.IsPathTakenAsync(userId, otherPath, fileId, cancellationToken);
        return await SaveAsync(userId, actor, operation, undoes, null, file, decide(file, taken), cancellationToken);
    }

    private async Task<MemoryOutcome<MemoryChange>> SaveAsync(
        Guid userId, MemoryActor actor, string operation, long? undoes, string? pathForMessages, StoredFile? file, ChangeTarget? target, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return MemoryOutcome<MemoryChange>.Refused(FileRefusals.NothingToChange);
        }

        if (Refusal(file, target) is { } refused)
        {
            return refused;
        }

        // A secret never reaches a file (spec 048).
        target = target.Deleted ? target : target with { Content = SecretFilter.Redact(target.Content) };
        var check = validator.Check(target.Path, target.Content);
        if (check.Error is not null && !target.Deleted)
        {
            return MemoryOutcome<MemoryChange>.Refused(check.Error);
        }

        if ((file is null || (!file.IsLive && !target.Deleted)) && await store.CountLiveAsync(userId, cancellationToken) >= Limits.MaxFilesPerUser)
        {
            return MemoryOutcome<MemoryChange>.Refused($"The memory is full ({Limits.MaxFilesPerUser} files); merge or delete files before adding new ones.");
        }

        var now = time.GetUtcNow();
        var next = new FileState(target.Path, target.Content, target.Sensitivity, target.Deleted, check.SizeBytes, check.LineCount, check.Header, check.RowCount);
        var saved = await store.SaveAsync(userId, file, next, actor, operation, undoes, now, cancellationToken);
        if (saved is null)
        {
            logger.LostRace(operation, actor.Author, userId);
            return MemoryOutcome<MemoryChange>.Refused(string.Format(CultureInfo.InvariantCulture, ChangedAtTheSameTime, pathForMessages ?? target.Path));
        }

        return MemoryOutcome<MemoryChange>.Ok(Change(saved, file, target, check, operation));
    }

    private static MemoryOutcome<MemoryChange>? Refusal(StoredFile? file, ChangeTarget target)
    {
        if (target.Refusal is not null)
        {
            return target.Missing ? MemoryOutcome<MemoryChange>.Missing(target.Refusal) : MemoryOutcome<MemoryChange>.Refused(target.Refusal);
        }

        // Every write passes here, undo and rollback too: the core stays at its path.
        return file is { IsLive: true } && MemoryPaths.IsCore(file.Path) && (target.Deleted || !MemoryPaths.IsCore(target.Path))
            ? MemoryOutcome<MemoryChange>.Refused(FileRefusals.CoreCannotBeDeleted)
            : null;
    }

    private static MemoryChange Change(SavedChange saved, StoredFile? file, ChangeTarget target, ContentCheck check, string operation)
    {
        var fromPath = file?.Path;
        var oldRows = file?.IsLive != false ? file?.RowCount ?? 0 : 0;
        var oldLines = file?.IsLive != false ? file?.LineCount ?? 0 : 0;
        var added = MemoryPaths.KindOf(target.Path) == MemoryFileKind.Collection
            ? (check.RowCount ?? 0) - oldRows
            : check.LineCount - oldLines;
        return new MemoryChange(
            saved.RevisionId, target.Path, operation, file is null, check.LineCount, check.RowCount, added,
            fromPath != target.Path ? fromPath : null, target.Sensitivity);
    }
}
