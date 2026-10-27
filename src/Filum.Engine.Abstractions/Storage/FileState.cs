namespace Filum.Engine.Abstractions.Storage;

/// <summary>What a file becomes after a change, as the engine decided it; the store only persists it.</summary>
public sealed record FileState(string Path, string Content, string Sensitivity, bool Deleted, int SizeBytes, int LineCount, string? Header, int? RowCount);
