namespace Filum.Engine.Abstractions.Options;

/// <summary>The limits of a person's memory and of one turn's use of it. Configuration, not code: section <c>Memory</c>.</summary>
public sealed class MemoryOptions
{
    /// <summary>The configuration section the options are read from.</summary>
    public const string SectionName = "Memory";

    /// <summary>A file is at most this many UTF-8 bytes.</summary>
    public int MaxFileBytes { get; set; } = 524_288;

    /// <summary>A collection (a .csv file, the facts among them) is at most this many UTF-8 bytes: its rows grow with use.</summary>
    public int MaxCollectionBytes { get; set; } = 33_554_432;

    /// <summary>A person has at most this many live files.</summary>
    public int MaxFilesPerUser { get; set; } = 500;

    /// <summary>The core is at most this many characters, and so is a package's prompt.</summary>
    public int MaxCoreChars { get; set; } = 10_000;

    /// <summary>One read returns at most this many lines.</summary>
    public int MaxReadLines { get; set; } = 500;

    /// <summary>One search of the files returns at most this many lines.</summary>
    public int MaxSearchResults { get; set; } = 50;

    /// <summary>One turn calls the memory's tools at most this many times.</summary>
    public int MaxToolCallsPerTurn { get; set; } = 20;

    /// <summary>The memory index given to the agent at every message stops at this many files...</summary>
    public int IndexMaxFiles { get; set; } = 60;

    /// <summary>...or at this many characters, whichever comes first.</summary>
    public int IndexMaxChars { get; set; } = 4_000;

    /// <summary>The list of enabled skills given to the agent at every message stops at this many skills.</summary>
    public int SkillListMax { get; set; } = 40;

    /// <summary>A skill file, header included, is at most this many characters.</summary>
    public int MaxSkillChars { get; set; } = 4_000;

    /// <summary>A conversation longer than this many characters is sent as a summary of its start and its latest messages.</summary>
    public int MaxHistoryChars { get; set; } = 60_000;

    /// <summary>An imported file has at most this many items.</summary>
    public int MaxImportItems { get; set; } = 5_000;

    /// <summary>An imported file is at most this many bytes.</summary>
    public int MaxImportBytes { get; set; } = 20 * 1_048_576;

    /// <summary>
    /// A search makes at most this many missing vectors, so it stays quick; the rest are made by a background pass
    /// (<c>IMemoryService.MakeVectorsAsync</c>).
    /// </summary>
    public int VectorsPerSearch { get; set; } = 32;

    /// <summary>A search of the log of events returns at most this many events.</summary>
    public int EventsSearchMax { get; set; } = 20;

    /// <summary>A listing of what was said (a search with <c>every</c>) shows at most this many messages (spec 057).</summary>
    public int EventsListMax { get; set; } = 100;

    /// <summary>A listing of what was said shows at most this many characters (spec 057).</summary>
    public int EventsListChars { get; set; } = 16_000;

    /// <summary>
    /// A search of what was said shows at most this many conversations, fewer when the scores drop sooner (spec 042).
    /// </summary>
    public int RecallSessionsMax { get; set; } = 5;

    /// <summary>A search of what was said shows at most this many characters of what was said (spec 042).</summary>
    public int RecallBudgetChars { get; set; } = 24_000;

    /// <summary>The person's brief at the start of a session is at most this many characters (spec 045).</summary>
    public int BriefChars { get; set; } = 8_000;

    /// <summary>What the memory holds about one message is at most this many characters (spec 045).</summary>
    public int RecallChars { get; set; } = 1_500;

    /// <summary>With a reranker, it orders again this many of a search's best matches (spec 042).</summary>
    public int RerankDepth { get; set; } = 20;

    /// <summary>
    /// Engine tools a host leaves out by name (for example a host whose rules forbid deleting): they are neither offered
    /// nor callable. Empty for the stock host.
    /// </summary>
    public string[] ExcludedTools { get; set; } = [];
}
