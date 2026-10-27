namespace Filum.Evals.Hosts.Wire;

internal sealed record MemoryFileDetailDto(
    string Path, string Kind, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount,
    string Content, MemoryOriginDto Origin);
