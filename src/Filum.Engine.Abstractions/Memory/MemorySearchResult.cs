namespace Filum.Engine.Abstractions.Memory;

/// <summary>The lines a search of the files found, up to the search limit.</summary>
/// <param name="Hits">The matching lines, file by file.</param>
/// <param name="Truncated">Whether more lines matched than were returned.</param>
public sealed record MemorySearchResult(IReadOnlyList<MemorySearchHit> Hits, bool Truncated);
