namespace Filum.Mcp;

internal static class McpConstants
{
    // At the start of every line written on stderr.
    public const string ProgramName = "filum-mcp";

    public const string ServerName = "filum";

    public static class Tools
    {
        public const string Log = "memory_log";
        public const string Consolidate = "memory_consolidate";
        public const string Import = "memory_import";

        // The core profile (spec 040), filum-mcp's default: reading, searching, writing files, facts, and logging what was said.
        public static readonly IReadOnlySet<string> Core = new HashSet<string>(StringComparer.Ordinal)
        {
            "memory_overview", "memory_read", "memory_search", "events_search", "events_read", "memory_write", "memory_edit", "memory_append",
            Consolidate, "fact_record", "fact_end", "facts_current", "memory_delete", Log,
        };

        // The server's own tools all write; logging or importing the same thing twice keeps it twice.
        public static readonly Filum.Engine.Abstractions.Tools.ToolTraits ServerTool = new(ReadOnly: false, Destructive: false, Idempotent: false);
    }

    public static class Limits
    {
        // memory_log refuses more; the hook keeps a longer text in parts.
        public const int MaxText = 8_000;

        public const int MaxPending = 100;

        // The text of the events one memory_consolidate call hands over.
        public const int MaxPendingChars = 16_000;

        public const int SampleLength = 200;

        // Claude Code cuts server instructions at 4,096 characters (spec 045): what is past it never reaches the model.
        public const int MaxInstructions = 4_095;

        // The rules the person set, read once at start for the instructions (spec 056); fewer when less room is left.
        public const int MaxRulesChars = 800;
    }

    public static class Variables
    {
        public const string Home = "FILUM_HOME";
        public const string Pack = "FILUM_PACK";
        public const string Embeddings = "FILUM_EMBEDDINGS";
        public const string Models = "FILUM_MODELS";
        public const string Tools = "FILUM_TOOLS";
        public const string Reranker = "FILUM_RERANKER";
        public const string Today = "FILUM_TODAY";

        public const string CoreProfile = "core";
        public const string FullProfile = "full";

        public const string Off = "off";
        public const string On = "on";
    }
}
