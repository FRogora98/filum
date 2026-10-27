using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Imports;

internal sealed class ImportService(IMemoryEventStore store, IOptions<MemoryOptions> options, ILogger<ImportService> logger, TimeProvider time)
{
    private const int SampleSize = 5;
    private const int BytesPerMegabyte = 1_048_576;
    private const string SourceIdPrefix = "source";
    private const string InvalidSensitivity = "The sensitivity must be normal, sensitive or private.";

    private MemoryOptions Limits => options.Value;

    // A preview writes nothing; over the limits, nothing is written either.
    public async Task<MemoryOutcome<ImportResult>> ImportAsync(Guid userId, ImportRequest request, CancellationToken cancellationToken)
    {
        if (Refusal(request) is { } refusal)
        {
            return MemoryOutcome<ImportResult>.Refused(refusal);
        }

        var format = string.IsNullOrWhiteSpace(request.Format) ? Importer.Detect(request.FileName, request.Content) : request.Format.Trim().ToLowerInvariant();
        if (format is null)
        {
            return MemoryOutcome<ImportResult>.Refused($"The format of {request.FileName} was not recognized; give one of {string.Join(", ", ImportFormat.All)}.");
        }

        var now = time.GetUtcNow();
        var parsed = Importer.Parse(format, request.FileName, request.Content, now);
        if ((parsed.Error ?? TooMany(request, parsed)) is { } error)
        {
            return MemoryOutcome<ImportResult>.Refused(error);
        }

        var source = Importer.IdOf($"{SourceIdPrefix}|{format}|{request.FileName}");
        var fresh = await FreshAsync(userId, parsed.Items, cancellationToken);
        var result = new ImportResult(format, source, parsed.Items.Count, fresh.Count, parsed.Items.Count - fresh.Count, parsed.Skipped,
            fresh.Take(SampleSize).Select(i => i.Item.Text).ToList(), Written: !request.Preview && fresh.Count > 0);
        if (request.Preview || fresh.Count == 0)
        {
            return MemoryOutcome<ImportResult>.Ok(result);
        }

        await AppendItemsAsync(userId, source, MemoryEventSource.Import, fresh, request.Sensitivity, now, cancellationToken);
        logger.Imported(userId, fresh.Count, format, result.Duplicates, parsed.Skipped);
        return MemoryOutcome<ImportResult>.Ok(result);
    }

    // Over the import limit, nothing is recorded.
    public async Task<MemoryOutcome<RecordedItems>> RecordItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<ImportItem> items, string sensitivity, CancellationToken cancellationToken)
    {
        if (!MemorySensitivity.All.Contains(sensitivity))
        {
            return MemoryOutcome<RecordedItems>.Refused(InvalidSensitivity);
        }

        if (items.Count > Limits.MaxImportItems)
        {
            return MemoryOutcome<RecordedItems>.Refused($"At most {Limits.MaxImportItems} items at once.");
        }

        var fresh = await FreshAsync(userId, items, cancellationToken);
        if (fresh.Count > 0)
        {
            await AppendItemsAsync(userId, sourceId, source, fresh, sensitivity, time.GetUtcNow(), cancellationToken);
        }

        return MemoryOutcome<RecordedItems>.Ok(new RecordedItems(fresh.Count));
    }

    private string? Refusal(ImportRequest request) =>
        Encoding.UTF8.GetByteCount(request.Content) > Limits.MaxImportBytes
            ? $"{request.FileName} is over {Limits.MaxImportBytes / BytesPerMegabyte} MB; split it into smaller files and import them one by one."
            : !MemorySensitivity.All.Contains(request.Sensitivity) ? InvalidSensitivity
            : null;

    private string? TooMany(ImportRequest request, ImportParse parsed) =>
        parsed.Items.Count > Limits.MaxImportItems
            ? $"{request.FileName} has {parsed.Items.Count} items, over the limit of {Limits.MaxImportItems}; split it into smaller files and import them one by one."
            : null;

    private async Task<List<(ImportItem Item, Guid Id)>> FreshAsync(Guid userId, IReadOnlyList<ImportItem> items, CancellationToken cancellationToken)
    {
        var known = (await store.EventsAsync(userId, new EventQuery(Kinds: [MemoryEventKind.Imported]), cancellationToken))
            .Select(e => e.MessageId)
            .OfType<Guid>()
            .ToHashSet();
        return items
            .Select(i => (Item: i, Id: Importer.IdOf(i.Identity)))
            .Where(i => known.Add(i.Id))
            .ToList();
    }

    private Task AppendItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<(ImportItem Item, Guid Id)> fresh, string sensitivity, DateTimeOffset now, CancellationToken cancellationToken) =>
        store.AppendEventsAsync(userId, fresh
            .Select(i => new NewMemoryEvent(MemoryEventKind.Imported, source, SecretFilter.Redact(i.Item.Text), i.Item.When ?? now, sourceId, i.Id, sensitivity))
            .ToList(), now, cancellationToken);
}
