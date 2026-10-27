namespace Filum.Engine.Abstractions.Skills;

/// <summary>A skill ready to save that a turn proposed; nothing is saved until the person accepts it.</summary>
public sealed record SkillProposal(string Name, string Description, string When, string Steps);
