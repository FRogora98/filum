namespace Filum.Engine.Abstractions.Collections;

/// <summary>A collection parsed and checked: its header, its rows, or why it is not a valid collection.</summary>
public sealed record CsvTable(IReadOnlyList<string> Header, IReadOnlyList<CsvRecord> Rows, string? Error)
{
    /// <summary>Whether the content is a valid collection; otherwise <see cref="Error"/> says why not.</summary>
    public bool IsValid => Error is null;
}
