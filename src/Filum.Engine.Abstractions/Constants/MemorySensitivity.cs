namespace Filum.Engine.Abstractions.Constants;

/// <summary>How carefully a file or an event is handled: what the agent may bring up, list or search.</summary>
public static class MemorySensitivity
{
    /// <summary>Used freely.</summary>
    public const string Normal = "normal";

    /// <summary>Used when it is relevant, never brought up unprompted.</summary>
    public const string Sensitive = "sensitive";

    /// <summary>Listed or searched only when the person asks for private content.</summary>
    public const string Private = "private";

    /// <summary>Every level, from the least to the most protected.</summary>
    public static readonly IReadOnlyList<string> All = [Normal, Sensitive, Private];
}
