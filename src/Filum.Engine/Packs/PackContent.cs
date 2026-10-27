namespace Filum.Engine.Packs;

// A package's files as read, before any check: the manifest and the prompt (null when missing), the files of skills/
// by file name, and those of memory/ by memory path, each list in the order the source gives.
internal sealed record PackContent(
    string? Manifest, string? Prompt, IReadOnlyList<(string Name, string Content)> Skills, IReadOnlyList<(string Path, string Content)> Memory);
