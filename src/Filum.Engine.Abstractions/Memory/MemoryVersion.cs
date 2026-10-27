namespace Filum.Engine.Abstractions.Memory;

/// <summary>A file as one revision left it, for reading it before bringing it back.</summary>
/// <param name="Id">The revision's id.</param>
/// <param name="Path">The file's path at that revision.</param>
/// <param name="Content">Its content at that revision.</param>
/// <param name="Deleted">Whether that revision deleted the file.</param>
/// <param name="CreatedAt">When that revision was saved.</param>
public sealed record MemoryVersion(long Id, string Path, string Content, bool Deleted, DateTimeOffset CreatedAt);
