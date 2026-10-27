namespace Filum.Engine.Abstractions.Collections;

/// <summary>One record of a CSV file and the line it starts on (1-based).</summary>
public sealed record CsvRecord(int Line, IReadOnlyList<string> Fields);
