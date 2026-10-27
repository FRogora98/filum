namespace Filum.Evals.Hosts.Wire;

internal sealed record MemoryFileDto(string Path, string Kind, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount)
{
    public const string CollectionKind = "collection";
    public const string DocumentKind = "document";
}
