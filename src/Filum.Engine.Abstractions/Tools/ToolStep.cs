namespace Filum.Engine.Abstractions.Tools;

/// <summary>
/// One thing a tool did while answering, as the tool reported it, never as the model told it. <paramref name="Data"/> is
/// what a host's tool surfaced for the person's app; the model never sees it.
/// </summary>
/// <param name="Kind">What kind of thing it did: one of the constants of this record.</param>
/// <param name="Tool">The tool's name.</param>
/// <param name="Path">The file it acted on, if any.</param>
/// <param name="Description">What it did, in the person's words, written by the code.</param>
/// <param name="DurationMs">How long the call took, in milliseconds.</param>
/// <param name="RevisionId">The revision a change made, if any.</param>
/// <param name="Error">Why the call was refused, when it was.</param>
/// <param name="Rows">The rows a collection has after the change, when it is one.</param>
/// <param name="Data">What a host's tool surfaced for the person's app.</param>
public sealed record ToolStep(string Kind, string Tool, string? Path, string Description, int DurationMs, long? RevisionId, string? Error = null, int? Rows = null, System.Text.Json.JsonElement? Data = null)
{
    /// <summary>A file or a skill was read.</summary>
    public const string Read = "read";

    /// <summary>The files or what was said were searched.</summary>
    public const string Searched = "searched";

    /// <summary>Files, skills, facts or a history were listed.</summary>
    public const string Listed = "listed";

    /// <summary>Numbers were computed in code.</summary>
    public const string Computed = "computed";

    /// <summary>A file was changed.</summary>
    public const string Wrote = "wrote";

    /// <summary>Something was proposed to the person, who decides.</summary>
    public const string Asked = "asked";

    /// <summary>The call was refused.</summary>
    public const string Failed = "failed";

    /// <summary>A host's own tool was used.</summary>
    public const string Used = "used";
}
