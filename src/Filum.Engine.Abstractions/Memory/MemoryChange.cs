namespace Filum.Engine.Abstractions.Memory;

/// <summary>A change that was applied: the revision it produced and what the file looks like now.</summary>
public sealed record MemoryChange(long RevisionId, string Path, string Operation, bool Created, int LineCount, int? RowCount, int Added, string? FromPath = null, string? Sensitivity = null);
