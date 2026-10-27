using Filum.Engine.Abstractions.Skills;

namespace Filum.Engine.Packs;

/// <summary>
/// A package: the data that makes the engine vertical, the same for every person. Its prompt is read at every
/// turn; its core, files and skills are given to a memory when it is created. Generic: the format knows no domain.
/// </summary>
public sealed record Pack(
    string Name, string Version, string Description, string? Prompt, string? Core, IReadOnlyList<Skill> Skills, IReadOnlyList<PackFile> Files)
{
    /// <summary>The version of the package format this engine reads.</summary>
    public const int Format = 1;
}
