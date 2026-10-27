namespace Filum.Engine.Abstractions.Skills;

/// <summary>A procedure the person keeps: a header (name, description, when it applies, on or off) and plain steps.</summary>
public sealed record Skill(string Name, string Description, string When, bool Enabled, string Steps);
