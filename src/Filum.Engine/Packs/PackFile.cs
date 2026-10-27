namespace Filum.Engine.Packs;

/// <summary>A file a package puts in every new memory.</summary>
public sealed record PackFile(string Path, string Content, string Sensitivity);
