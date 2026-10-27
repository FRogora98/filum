using System.Text.Json;

namespace Filum.Mcp.Hook;

// What a hook gives the model (spec 045): Claude Code adds `additionalContext` to the context, and nothing else of the
// answer reaches the model. An empty context answers nothing.
internal static class HookAnswer
{
    public const string SessionStart = "SessionStart";
    public const string UserPromptSubmit = "UserPromptSubmit";

    public static async Task WriteAsync(TextWriter output, string hookEventName, string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return;
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(new { hookSpecificOutput = new { hookEventName, additionalContext = context } }));
        await output.FlushAsync();
    }
}
