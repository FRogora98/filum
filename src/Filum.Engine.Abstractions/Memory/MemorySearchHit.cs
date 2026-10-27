namespace Filum.Engine.Abstractions.Memory;

/// <summary>One line of a file that contains the searched text.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Line">The line's number, from 1.</param>
/// <param name="Text">The line's text.</param>
public sealed record MemorySearchHit(string Path, int Line, string Text);
