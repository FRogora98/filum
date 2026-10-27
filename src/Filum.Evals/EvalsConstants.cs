namespace Filum.Evals;

internal static class EvalsConstants
{
    public static class ExitCodes
    {
        public const int Success = 0;

        public const int CannotStart = 1;

        public const int OverCap = 2;
    }

    public static class Http
    {
        public const string BearerScheme = "Bearer";

        public const int FirstClientError = 400;

        public const int FirstServerError = 500;
    }

    public static class HostRoutes
    {
        public const string Models = "/models";
        public const string Conversations = "/conversations";
        public const string Consolidate = "/memory/consolidate";
        public const string MemoryFiles = "/memory/files";
    }

    public static class Shell
    {
        public const string Windows = "cmd";
        public const string WindowsRun = "/c";
        public const string Unix = "sh";
        public const string UnixRun = "-c";
    }

    public static class SetupPlaceholders
    {
        public const string Email = "{email}";
        public const string Person = "{person}";
    }

    public static class ModelsFileFields
    {
        public const string Models = "Models";
        public const string Providers = "Providers";
        public const string Id = "Id";
        public const string Provider = "Provider";
        public const string ProviderModel = "ProviderModel";
        public const string BaseUrl = "BaseUrl";
        public const string InputPrice = "InputPricePerMillionUsd";
        public const string OutputPrice = "OutputPricePerMillionUsd";

        // A provider's key is read from <PROVIDER>_API_KEY.
        public const string ApiKeySuffix = "_API_KEY";
    }

    public static class AgentStream
    {
        public const string Type = "type";
        public const string Model = "model";
        public const string Result = "result";
        public const string IsError = "is_error";
        public const string Message = "message";
        public const string Content = "content";
        public const string Name = "name";
        public const string Input = "input";

        // A skill tool's inputs (skill_use, skill_propose).
        public const string Description = "description";
        public const string When = "when";
        public const string Steps = "steps";

        public const string SystemType = "system";
        public const string AssistantType = "assistant";
        public const string ResultType = "result";
        public const string ToolUseType = "tool_use";
    }

    public static class AgentArguments
    {
        public const string Print = "-p";
        public const string OutputFormat = "--output-format";
        public const string StreamJson = "stream-json";
        public const string Verbose = "--verbose";
        public const string StrictMcpConfig = "--strict-mcp-config";
        public const string McpConfig = "--mcp-config";
        public const string Tools = "--tools";

        public const string NoTools = "";

        public const string AllowedTools = "--allowedTools";
        public const string Model = "--model";
        public const string Settings = "--settings";
        public const string SessionId = "--session-id";
        public const string Resume = "--resume";

        public const string AnyTool = "*";
    }

    // What keeps a run's model from seeing the person running it: their account, Claude Code's own memory and CLAUDE.md files.
    public static class Isolation
    {
        public const string ConfigDirectory = "CLAUDE_CONFIG_DIR";
        public const string DisableAutoMemory = "CLAUDE_CODE_DISABLE_AUTO_MEMORY";
        public const string DisableClaudeMds = "CLAUDE_CODE_DISABLE_CLAUDE_MDS";
        public const string On = "1";
        public const string OAuthToken = "CLAUDE_CODE_OAUTH_TOKEN";
        public const string ApiKey = "ANTHROPIC_API_KEY";

        public const string UserFolder = ".claude";
        public const string CredentialsFile = ".credentials.json";
        public const string ConfigFile = ".claude.json";
        public const string Login = "claudeAiOauth";
        public const string Account = "oauthAccount";
        public const string ExpiresAt = "expiresAt";
        public const string FolderPrefix = "filum-evals-claude-";

        public const string WarmUp = "Reply ok.";
        public const string Check = "What is my email address, my name and my organisation? Is a persistent memory directory or a MEMORY.md described in your instructions? Answer each with NONE when you do not know.";

        // Shorter values (an empty or blanked field) are not looked for in an answer.
        public const int MinIdentityLength = 4;

        // A login that ends sooner is renewed in the user's own config before it is copied.
        public const int RenewMinutes = 30;

        // How many times the check is asked before the run is refused.
        public const int Checks = 3;
    }

    public static class LongMemEvalSystems
    {
        public const string None = "none";
        public const string Naive = "naive";
        public const string Filum = "filum";
        public const string Full = "full";
    }

    public static class Folders
    {
        public const string Memory = "memory";
        public const string Question = "question";
    }
}
