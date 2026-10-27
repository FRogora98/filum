using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// Reads a person's files: lists, reads and searches them, and builds the map of the memory. A role of
/// <see cref="IMemoryService"/>, for callers that need only this.
/// </summary>
public interface IMemoryReader
{
    /// <summary>The live files under <paramref name="prefix"/>, private ones only when asked.</summary>
    Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken);

    /// <summary>The lines of a file from <paramref name="fromLine"/> to <paramref name="toLine"/>, within the read limit.</summary>
    Task<MemoryOutcome<MemoryReadResult>> ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken);

    /// <summary>
    /// The map of the memory, for the agent's instructions: every live, non-private file but the core, newest first, one
    /// line each with what it holds, until the file or character limit; then how many are left out.
    /// </summary>
    Task<string> BuildIndexAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A whole file with what the person sees about it, private files included (they are the person's own).</summary>
    Task<MemoryOutcome<MemoryFileContent>> GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <summary>A collection, parsed and checked, for computing over it.</summary>
    Task<MemoryOutcome<CsvTable>> ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken);

    /// <summary>The live files whose content contains <paramref name="query"/>, with the matching lines.</summary>
    Task<MemoryOutcome<MemorySearchResult>> SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken);
}
