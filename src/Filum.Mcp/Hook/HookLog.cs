using Filum.Engine.Abstractions.Events;
using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Startup;

namespace Filum.Mcp.Hook;

// Kept in the log before the model reads it, whatever the model then does. To a hook's JSON (Claude Code's
// UserPromptSubmit), it also answers what the memory holds about the message (spec 045), found before the message is
// kept so it never recalls itself; plain text gets no answer. Always exit code 0: a failure is a line on stderr, never
// a blocked message.
internal static class HookLog
{
    public const string Command = "log";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextReader input, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        try
        {
            var home = HookInput.Home(args);
            var raw = await input.ReadToEndAsync(cancellationToken);
            var (text, session) = HookInput.Read(raw);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var recall = await KeepAsync(home, text, session, cancellationToken);
                if (HookInput.IsHook(raw))
                {
                    await HookAnswer.WriteAsync(output, HookAnswer.UserPromptSubmit, recall);
                }
            }
        }
        catch (Exception e)
        {
            // Whatever failed, the person's message goes through: the hook reports it and never blocks the host.
            await errors.WriteLineAsync($"{McpConstants.ProgramName} {Command}: nothing kept ({e.GetType().Name}: {e.Message})");
        }

        return 0;
    }

    // What the memory held about the message before it was kept.
    private static async Task<string> KeepAsync(string home, string text, string? session, CancellationToken cancellationToken)
    {
        var (store, engine) = McpClock.Open(home);
        using var _ = store;
        using var __ = engine;
        IMemoryBriefing briefing = engine.Memory;
        var recall = await briefing.RecallAsync(store.Owner, text, null, cancellationToken);
        IMemoryEvents events = engine.Memory;
        var conversation = HookInput.Conversation(session);
        foreach (var part in TextParts.Split(text.Trim()))
        {
            await events.RecordAsync(store.Owner, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Host, part, ConversationId: conversation), cancellationToken);
        }

        return recall;
    }
}
