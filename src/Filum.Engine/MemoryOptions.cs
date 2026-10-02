namespace Filum.Engine;

/// <summary>The limits of a person's memory and of one turn's use of it. Configuration, not code: section <c>Memory</c>.</summary>
public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    public int MaxFileBytes { get; set; } = 524_288;

    public int MaxFilesPerUser { get; set; } = 500;

    public int MaxCoreChars { get; set; } = 10_000;

    public int MaxReadLines { get; set; } = 500;

    public int MaxSearchResults { get; set; } = 50;

    public int MaxToolCallsPerTurn { get; set; } = 20;

    /// <summary>The memory index given to the agent at every message stops at this many files...</summary>
    public int IndexMaxFiles { get; set; } = 60;

    /// <summary>...or at this many characters, whichever comes first.</summary>
    public int IndexMaxChars { get; set; } = 4_000;

    /// <summary>The list of enabled skills given to the agent at every message stops at this many skills.</summary>
    public int SkillListMax { get; set; } = 40;

    /// <summary>A skill file, header included, is at most this many characters.</summary>
    public int MaxSkillChars { get; set; } = 4_000;

    /// <summary>A conversation longer than this many characters is sent as a summary of its start and its latest messages (spec 030).</summary>
    public int MaxHistoryChars { get; set; } = 60_000;

    /// <summary>A search of the log of events returns at most this many events (spec 030).</summary>
    public int EventsSearchMax { get; set; } = 8;

    /// <summary>
    /// Engine tools a host leaves out by name (for example a host whose rules forbid deleting): they are neither offered
    /// nor callable. Empty for the stock host.
    /// </summary>
    public string[] ExcludedTools { get; set; } = [];
}
