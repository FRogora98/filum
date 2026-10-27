using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Filum.Mcp.Configuration;

namespace Filum.Mcp.Hook;

internal static class HookInput
{
    private const string HomeOption = "--home";
    private const string PromptProperty = "prompt";
    private const string SessionProperty = "session_id";
    private const string ConversationSeed = "filum-hook:";

    public static string Home(IReadOnlyList<string> args) =>
        args.SkipWhile(a => a != HomeOption).Skip(1).FirstOrDefault() ?? McpOptions.FromEnvironment().Home;

    // A hook's JSON (a prompt in an object), as opposed to plain text piped by another host.
    public static bool IsHook(string input)
    {
        try
        {
            using var json = JsonDocument.Parse(input);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty(PromptProperty, out var prompt) && prompt.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // The whole input when it is not a hook's JSON.
    public static (string Text, string? Session) Read(string input)
    {
        try
        {
            using var json = JsonDocument.Parse(input);
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(PromptProperty, out var prompt) && prompt.ValueKind == JsonValueKind.String)
            {
                var session = root.TryGetProperty(SessionProperty, out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                return (prompt.GetString() ?? string.Empty, session);
            }
        }
        catch (JsonException)
        {
            // Not a hook's JSON: a host that pipes the plain text has it kept as it is.
        }

        return (input, null);
    }

    // The same for every prompt of one session; a new one without a session.
    public static Guid Conversation(string? session) =>
        session is null ? Guid.NewGuid() : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(ConversationSeed + session)).AsSpan(0, 16));
}
