using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// Brings items from outside into a person's log: files the person imports and live sources. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryImports
{
    /// <summary>
    /// Imports a file: every new item becomes an <c>imported</c> event with its own date, the file's source id and the
    /// chosen sensitivity; items already in the log are skipped. A preview writes nothing; over the limits, neither.
    /// </summary>
    Task<MemoryOutcome<ImportResult>> ImportAsync(Guid userId, ImportRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Records items from a live source as <c>imported</c> events with <paramref name="sourceId"/>, by the rules of an
    /// import, and says how many were new; refused for an unknown sensitivity or over the import limit.
    /// </summary>
    Task<MemoryOutcome<RecordedItems>> RecordItemsAsync(Guid userId, Guid sourceId, string source, IReadOnlyList<ImportItem> items, string sensitivity, CancellationToken cancellationToken);
}
