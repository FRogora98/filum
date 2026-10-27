using System.ComponentModel;
using System.Globalization;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Session;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.Tools;

// Keeps what the person said, with its date, without changing a file.
internal sealed class LogToolStrategy(McpEngines engines, OpenedMemory memory, ServerSession session) : IServerToolStrategy
{
    private const string Description = "Keep what the person said that matters, in their words, with its date: a fact about them or their life, a decision, a plan, a wish. It is kept as it was said, searchable with events_search, and tidied into the memory later by memory_consolidate; it changes no file. Call it whenever they tell you such a thing, even when you also save it in a file.";

    public AIFunction Function() => AIFunctionFactory.Create(LogAsync, McpConstants.Tools.Log, Description);

    public async Task<string> LogAsync(
        [Description("What the person said, in their words: one fact, decision, plan or wish about them or their life.")] string text,
        [Description("When it happened or was said, yyyy-MM-dd or an ISO date and time; now when not given.")] string? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > McpConstants.Limits.MaxText)
        {
            throw new McpException($"Refused: the text must be between 1 and {McpConstants.Limits.MaxText} characters.");
        }

        var said = new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Host, text.Trim(), When(occurredAt), session.Id);
        IMemoryEvents events = engines.Current.Memory;
        var logged = await events.RecordAsync(memory.Owner, said, cancellationToken);
        // What the model reads right before it answers: kept words are not a saved fact (spec 055).
        return $"Logged (event {logged.Id}): their words are kept, but nothing is saved yet. To remember it, use fact_record (a fact, a rule, an open item) or a file; until then, do not say it is saved.";
    }

    private static DateTimeOffset? When(string? occurredAt)
    {
        if (string.IsNullOrWhiteSpace(occurredAt))
        {
            return null;
        }

        return DateTimeOffset.TryParse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : throw new McpException("Refused: occurredAt must be a date, yyyy-MM-dd, or an ISO date and time.");
    }
}
