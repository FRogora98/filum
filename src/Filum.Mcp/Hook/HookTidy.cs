using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Startup;

namespace Filum.Mcp.Hook;

// Claude Code's Stop hook (spec 046): when enough of what was said is waiting to be tidied, the agent is asked once per
// session to call memory_consolidate before it stops, after its answer is already shown. So what a turn missed is caught
// without depending on the model remembering to tidy. It never fails or loops: on any error, a second stop of the same
// turn (stop_hook_active) or a session already asked, it answers nothing and exits 0.
internal static class HookTidy
{
    public const string Command = "tidy";

    // Fewer waiting messages than this are left for later.
    public const int Backlog = 10;

    private const string StopHookActive = "stop_hook_active";
    private const string SessionProperty = "session_id";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        try
        {
            var home = HookInput.Home(args);
            var (session, again) = Read(await input.ReadToEndAsync(cancellationToken));
            var marker = Marker(home, session);
            if (again || !Directory.Exists(home) || File.Exists(marker))
            {
                return 0;
            }

            var (store, engine) = McpClock.Open(home);
            using var _ = store;
            using var __ = engine;
            IMemoryConsolidation memory = engine.Memory;
            var waiting = (await memory.PendingAsync(store.Owner, Backlog + 1, cancellationToken)).Count;
            if (waiting <= Backlog)
            {
                return 0;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            await File.WriteAllTextAsync(marker, string.Empty, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                decision = "block",
                reason = $"Before you stop: more than {Backlog} messages the person said are not tidied into their memory yet. Call {McpConstants.Tools.Consolidate} now and do what it returns, then stop. Do not repeat your answer.",
            }));
        }
        catch (Exception e)
        {
            await errors.WriteLineAsync($"{McpConstants.ProgramName} {Command}: not asked ({e.GetType().Name}: {e.Message})");
        }

        return 0;
    }

    private static (string? Session, bool Again) Read(string input)
    {
        try
        {
            using var json = JsonDocument.Parse(input);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, false);
            }

            var session = root.TryGetProperty(SessionProperty, out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
            return (session, root.TryGetProperty(StopHookActive, out var active) && active.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    // Once per session and memory folder; without a session, once per run of the process that asks.
    private static string Marker(string home, string? session) =>
        Path.Combine(Path.GetTempPath(), McpConstants.ProgramName, "tidied",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Path.GetFullPath(home)}|{session ?? Guid.NewGuid().ToString()}")))[..32]);
}
