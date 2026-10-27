namespace Filum.Engine.Abstractions.Skills;

/// <summary>A skill as the memory holds it.</summary>
public sealed record SkillFile(Skill Skill, string Path, string Sensitivity, DateTimeOffset UpdatedAt);
