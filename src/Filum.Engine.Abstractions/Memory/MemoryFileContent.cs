namespace Filum.Engine.Abstractions.Memory;

/// <summary>A whole file: what the person sees about it, and its content.</summary>
/// <param name="Info">What the person sees about the file.</param>
/// <param name="Content">Its whole content.</param>
public sealed record MemoryFileContent(MemoryFileInfo Info, string Content);
