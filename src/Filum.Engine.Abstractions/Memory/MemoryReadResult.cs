namespace Filum.Engine.Abstractions.Memory;

/// <summary>A page of a file's lines, numbered from 1.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Sensitivity">Its sensitivity.</param>
/// <param name="FromLine">The number of the first line returned.</param>
/// <param name="ToLine">The number of the last line returned.</param>
/// <param name="TotalLines">How many lines the file has.</param>
/// <param name="Lines">The lines returned, without their line breaks.</param>
public sealed record MemoryReadResult(string Path, string Sensitivity, int FromLine, int ToLine, int TotalLines, IReadOnlyList<string> Lines);
