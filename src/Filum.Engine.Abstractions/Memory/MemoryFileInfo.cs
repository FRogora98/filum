namespace Filum.Engine.Abstractions.Memory;

/// <summary>What the person sees about a live file without its content.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="SizeBytes">Its size in UTF-8 bytes.</param>
/// <param name="LineCount">How many lines it has.</param>
/// <param name="Sensitivity">Its sensitivity.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="Header">A collection's fields, comma-separated; null for a document.</param>
/// <param name="RowCount">A collection's rows; null for a document.</param>
public sealed record MemoryFileInfo(string Path, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount);
