namespace Filum.Agent;

/// <summary>How a turn checks that the agent did what it says. Configuration, not code: section <c>Reliability</c>.</summary>
public sealed class ReliabilityOptions
{
    public const string SectionName = "Reliability";

    /// <summary>The catalog model that checks, after a turn without changes, whether one was asked for or claimed; empty = no check.</summary>
    public string CheckModel { get; set; } = "gpt-5.4-mini";

    /// <summary>A stronger catalog model for one last attempt when the second attempt still claims a change it did not make; empty = off.</summary>
    public string EscalationModel { get; set; } = string.Empty;
}
