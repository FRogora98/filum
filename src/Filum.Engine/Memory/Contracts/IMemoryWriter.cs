using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// Changes a person's files and collections; every change adds a revision. A role of <see cref="IMemoryService"/>, for
/// callers that need only this.
/// </summary>
public interface IMemoryWriter
{
    /// <summary>Creates a document or replaces its whole content.</summary>
    Task<MemoryOutcome<MemoryChange>> WriteAsync(Guid userId, MemoryActor actor, string? path, string? content, CancellationToken cancellationToken);

    /// <summary>Replaces the one exact occurrence of <paramref name="oldText"/> with <paramref name="newText"/>.</summary>
    Task<MemoryOutcome<MemoryChange>> EditAsync(Guid userId, MemoryActor actor, string? path, string? oldText, string? newText, CancellationToken cancellationToken);

    /// <summary>Adds text at the end of a document, creating it when it is missing.</summary>
    Task<MemoryOutcome<MemoryChange>> AppendAsync(Guid userId, MemoryActor actor, string? path, string? text, CancellationToken cancellationToken);

    /// <summary>
    /// Adds rows given as field → value to a collection; the code writes the CSV. A missing collection is created
    /// with the rows' fields; a field the collection does not have is refused, listing its fields.
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken);

    /// <summary>Changes, in every row whose fields equal all of <paramref name="where"/>, the fields of <paramref name="set"/>.</summary>
    Task<MemoryOutcome<MemoryChange>> UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken);

    /// <summary>Removes every row whose fields equal all of <paramref name="where"/>.</summary>
    Task<MemoryOutcome<MemoryChange>> RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken);

    /// <summary>Adds a field to a collection; existing rows get an empty value.</summary>
    Task<MemoryOutcome<MemoryChange>> AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken);

    /// <summary>Deletes a file; it stays in the history and can be undone. The core cannot be deleted.</summary>
    Task<MemoryOutcome<MemoryChange>> DeleteAsync(Guid userId, MemoryActor actor, string? path, CancellationToken cancellationToken);

    /// <summary>Renames or moves a file, keeping its history.</summary>
    Task<MemoryOutcome<MemoryChange>> MoveAsync(Guid userId, MemoryActor actor, string? from, string? to, CancellationToken cancellationToken);

    /// <summary>Sets a file's sensitivity: normal, sensitive or private.</summary>
    Task<MemoryOutcome<MemoryChange>> SetSensitivityAsync(Guid userId, MemoryActor actor, string? path, string? level, CancellationToken cancellationToken);
}
