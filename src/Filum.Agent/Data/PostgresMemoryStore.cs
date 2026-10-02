using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Filum.Agent;

/// <summary>
/// The engine's memory in Postgres, in the host's database. Every query is filtered by the user; each call uses its
/// own short-lived context. A change saves the file and its revision in one transaction, and the file's version is
/// its concurrency token: two changes from the same version cannot both win.
/// </summary>
public sealed class PostgresMemoryStore(IFilumDb filumDb) : IMemoryStore
{
    public async Task<StoredFile?> FindLiveAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var file = await Live(db, userId).AsNoTracking().FirstOrDefaultAsync(f => f.Path == path, cancellationToken);
        return file is null ? null : Stored(file);
    }

    public async Task<StoredFile?> FindAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var file = await db.Set<MemoryFile>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId, cancellationToken);
        return file is null ? null : Stored(file);
    }

    public async Task<IReadOnlyList<StoredFile>> ListLiveAsync(Guid userId, string? prefix, bool includePrivate, bool withContent, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var files = Visible(db, userId, prefix, includePrivate);
        if (!withContent)
        {
            // The index and the listings need no content: it can be large, and they run at every message.
            return await files
                .Select(f => new StoredFile(f.Id, f.Path, string.Empty, f.Sensitivity, f.SizeBytes, f.LineCount, f.Header, f.RowCount, f.Version, f.LatestRevisionId, f.CreatedAt, f.UpdatedAt, f.DeletedAt))
                .ToListAsync(cancellationToken);
        }

        return (await files.AsNoTracking().ToListAsync(cancellationToken)).Select(Stored).ToList();
    }

    public async Task<IReadOnlyList<StoredFile>> SearchAsync(Guid userId, string text, string? prefix, bool includePrivate, CancellationToken cancellationToken)
    {
        var pattern = "%" + text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
        await using var db = filumDb.CreateContext();
        var files = await Visible(db, userId, prefix, includePrivate)
            .Where(f => EF.Functions.ILike(f.Content, pattern, @"\"))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return files.Select(Stored).ToList();
    }

    public async Task<int> CountLiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        return await Live(db, userId).CountAsync(cancellationToken);
    }

    public async Task<bool> IsPathTakenAsync(Guid userId, string path, Guid? exceptFileId, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        return await Live(db, userId).AnyAsync(f => f.Path == path && (exceptFileId == null || f.Id != exceptFileId), cancellationToken);
    }

    public async Task<StoredRevision?> GetRevisionAsync(Guid userId, long revisionId, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var revision = await db.Set<MemoryRevision>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId && r.UserId == userId, cancellationToken);
        return revision is null ? null : Stored(revision);
    }

    public async Task<IReadOnlyList<StoredRevision>> RevisionsOfFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        return (await db.Set<MemoryRevision>().AsNoTracking()
                .Where(r => r.UserId == userId && r.FileId == fileId)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken))
            .Select(Stored)
            .ToList();
    }

    public async Task<IReadOnlyList<StoredRevision>> RevisionsAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        return (await db.Set<MemoryRevision>().AsNoTracking()
                .Where(r => r.UserId == userId && revisionIds.Contains(r.Id))
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken))
            .Select(Stored)
            .ToList();
    }

    public async Task<IReadOnlySet<long>> UndoneAmongAsync(Guid userId, IReadOnlyCollection<long> revisionIds, CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return new HashSet<long>();
        }

        await using var db = filumDb.CreateContext();
        var undone = await db.Set<MemoryRevision>()
            .Where(r => r.UserId == userId && r.Operation == MemoryOperation.Undo && r.UndoesRevisionId != null && revisionIds.Contains(r.UndoesRevisionId.Value))
            .Select(r => r.UndoesRevisionId!.Value)
            .ToListAsync(cancellationToken);
        return undone.ToHashSet();
    }

    public async Task<SavedChange?> SaveAsync(Guid userId, StoredFile? current, FileState next, MemoryActor actor, string operation, long? undoesRevisionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        MemoryFile file;
        if (current is null)
        {
            file = new MemoryFile { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now };
            db.Add(file);
        }
        else
        {
            // Attached as it was read: the update is conditional on that version, so a change made meanwhile wins.
            file = new MemoryFile
            {
                Id = current.Id,
                UserId = userId,
                Path = current.Path,
                Content = current.Content,
                Sensitivity = current.Sensitivity,
                SizeBytes = current.SizeBytes,
                LineCount = current.LineCount,
                Header = current.Header,
                RowCount = current.RowCount,
                LatestRevisionId = current.LatestRevisionId,
                Version = current.Version,
                CreatedAt = current.CreatedAt,
                UpdatedAt = current.UpdatedAt,
                DeletedAt = current.DeletedAt
            };
            db.Attach(file);
        }

        file.Path = next.Path;
        file.Content = next.Content;
        file.Sensitivity = next.Sensitivity;
        file.SizeBytes = next.SizeBytes;
        file.LineCount = next.LineCount;
        file.Header = next.Header;
        file.RowCount = next.RowCount;
        file.UpdatedAt = now;
        file.DeletedAt = next.Deleted ? now : null;
        file.Version++;

        var revision = new MemoryRevision
        {
            FileId = file.Id,
            UserId = userId,
            Path = next.Path,
            Content = next.Content,
            Sensitivity = next.Sensitivity,
            Deleted = next.Deleted,
            Operation = operation,
            Author = actor.Author,
            ConversationId = actor.ConversationId,
            MessageId = actor.MessageId,
            UndoesRevisionId = undoesRevisionId,
            CreatedAt = now
        };
        db.Add(revision);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            file.LatestRevisionId = revision.Id;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            return null;
        }

        return new SavedChange(revision.Id, file.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        return await db.Set<Conversation>()
            .Where(c => c.UserId == userId && conversationIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Title, cancellationToken);
    }

    public async Task<MemoryEvent> AppendEventAsync(Guid userId, NewMemoryEvent next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var row = new MemoryEventRow
        {
            UserId = userId,
            OccurredAt = next.OccurredAt ?? now,
            RecordedAt = now,
            Kind = next.Kind,
            Source = next.Source,
            ConversationId = next.ConversationId,
            MessageId = next.MessageId,
            Text = next.Text,
            Sensitivity = next.Sensitivity,
            Sources = [.. next.Sources ?? []],
            Revisions = [.. next.Revisions ?? []]
        };
        db.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Stored(row);
    }

    public async Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        var rows = db.Set<MemoryEventRow>().AsNoTracking().Where(e => e.UserId == userId);
        if (query.AfterId is { } after)
        {
            rows = rows.Where(e => e.Id > after);
        }

        if (query.Kinds is { } kinds)
        {
            var list = kinds.ToList();
            rows = rows.Where(e => list.Contains(e.Kind));
        }

        if (query.ConversationId is { } conversation)
        {
            rows = rows.Where(e => e.ConversationId == conversation);
        }

        if (query.From is { } from)
        {
            rows = rows.Where(e => e.OccurredAt >= from);
        }

        if (query.Before is { } before)
        {
            rows = rows.Where(e => e.OccurredAt < before);
        }

        if (!query.IncludePrivate)
        {
            rows = rows.Where(e => e.Sensitivity != MemorySensitivity.Private);
        }

        return (await rows.OrderBy(e => e.Id).ToListAsync(cancellationToken)).Select(Stored).ToList();
    }

    public async Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        await using var db = filumDb.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var events = eventIds.ToList();
        var files = fileIds.ToList();
        await db.Set<MemoryEventRow>().Where(e => e.UserId == userId && events.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<MemoryRevision>().Where(r => r.UserId == userId && files.Contains(r.FileId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<MemoryFile>().Where(f => f.UserId == userId && files.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static MemoryEvent Stored(MemoryEventRow e) =>
        new(e.Id, e.OccurredAt, e.RecordedAt, e.Kind, e.Source, e.ConversationId, e.MessageId, e.Text, e.Sensitivity, e.Sources, e.Revisions);

    private static IQueryable<MemoryFile> Live(DbContext db, Guid userId) =>
        db.Set<MemoryFile>().Where(f => f.UserId == userId && f.DeletedAt == null);

    private static IQueryable<MemoryFile> Visible(DbContext db, Guid userId, string? prefix, bool includePrivate)
    {
        var files = Live(db, userId);
        if (!includePrivate)
        {
            files = files.Where(f => f.Sensitivity != MemorySensitivity.Private);
        }

        return string.IsNullOrEmpty(prefix) || prefix == "/" ? files : files.Where(f => f.Path.StartsWith(prefix));
    }

    private static StoredFile Stored(MemoryFile f) =>
        new(f.Id, f.Path, f.Content, f.Sensitivity, f.SizeBytes, f.LineCount, f.Header, f.RowCount, f.Version, f.LatestRevisionId, f.CreatedAt, f.UpdatedAt, f.DeletedAt);

    private static StoredRevision Stored(MemoryRevision r) =>
        new(r.Id, r.FileId, r.Path, r.Content, r.Sensitivity, r.Deleted, r.Operation, r.Author, r.ConversationId, r.MessageId, r.UndoesRevisionId, r.CreatedAt);
}
