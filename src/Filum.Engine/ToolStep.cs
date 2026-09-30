namespace Filum.Engine;

/// <summary>
/// One thing a tool did while answering, as the tool reported it, never as the model told it. <paramref name="Data"/> is
/// what a host's tool surfaced for the person's app (spec 017); the model never sees it.
/// </summary>
public sealed record ToolStep(string Kind, string Tool, string? Path, string Description, int DurationMs, long? RevisionId, string? Error = null, int? Rows = null, System.Text.Json.JsonElement? Data = null)
{
    public const string Read = "read";
    public const string Searched = "searched";
    public const string Listed = "listed";
    public const string Computed = "computed";
    public const string Wrote = "wrote";
    public const string Asked = "asked";
    public const string Failed = "failed";

    /// <summary>A host's own tool was used (spec 017).</summary>
    public const string Used = "used";
}

/// <summary>A skill ready to save that a turn proposed; nothing is saved until the person accepts it.</summary>
public sealed record SkillProposal(string Name, string Description, string When, string Steps);
