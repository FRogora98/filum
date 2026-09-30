namespace Filum.Engine;

/// <summary>One thing a tool did on the person's memory, as the tool reported it, never as the model told it.</summary>
public sealed record ToolStep(string Kind, string Tool, string? Path, string Description, int DurationMs, long? RevisionId, string? Error = null, int? Rows = null)
{
    public const string Read = "read";
    public const string Searched = "searched";
    public const string Listed = "listed";
    public const string Computed = "computed";
    public const string Wrote = "wrote";
    public const string Asked = "asked";
    public const string Failed = "failed";
}

/// <summary>A skill ready to save that a turn proposed; nothing is saved until the person accepts it.</summary>
public sealed record SkillProposal(string Name, string Description, string When, string Steps);
