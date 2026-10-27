using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Startup;

namespace Filum.Mcp.Hook;

// Claude Code's SessionStart hook (spec 045): the person's brief is in the context before the first message, with no
// tool call. It never fails a session: on any error it answers nothing, says why on stderr and exits 0. A memory folder
// that does not exist yet has nothing to tell and is not created.
internal static class HookBrief
{
    public const string Command = "brief";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        try
        {
            var home = HookInput.Home(args);
            if (!Directory.Exists(home))
            {
                return 0;
            }

            var (store, engine) = McpClock.Open(home);
            using var _ = store;
            using var __ = engine;
            IMemoryBriefing memory = engine.Memory;
            await HookAnswer.WriteAsync(output, HookAnswer.SessionStart, await memory.BriefAsync(store.Owner, null, cancellationToken));
        }
        catch (Exception e)
        {
            await errors.WriteLineAsync($"{McpConstants.ProgramName} {Command}: no brief ({e.GetType().Name}: {e.Message})");
        }

        return 0;
    }
}
