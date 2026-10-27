using System.ComponentModel;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Consolidation;
using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Session;
using Filum.Mcp.Search;
using Microsoft.Extensions.AI;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.Tools;

// The pass closes when the model calls it again with the last id.
internal sealed class ConsolidateToolStrategy(McpEngines engines, OpenedMemory memory, VectorMaker vectors) : IServerToolStrategy
{
    private const string Description = "Tidy the person's memory: it gives what was said since the last time with the rules to follow; do it with the usual tools, then call it again with through to close. Call it once a session, after answering the person's first request, and whenever the person asks to tidy the memory.";
    private const string NothingWaiting = "Nothing is waiting to be tidied.";

    public AIFunction Function() => AIFunctionFactory.Create(ConsolidateAsync, McpConstants.Tools.Consolidate, Description);

    public async Task<string> ConsolidateAsync(
        [Description("Leave it out to get what is waiting; after tidying it, call again with the id given, to close the pass.")] long? through = null,
        CancellationToken cancellationToken = default)
    {
        IMemoryConsolidation service = engines.Current.Memory;
        var pending = await service.PendingAsync(memory.Owner, McpConstants.Limits.MaxPending, cancellationToken);
        if (through is { } last)
        {
            var read = pending.Where(e => e.Id <= last).Select(e => e.Id).ToList();
            if (read.Count == 0)
            {
                return "Nothing up to that id was waiting.";
            }

            await service.CloseConsolidationAsync(memory.Owner, read, [], cancellationToken);
            _ = vectors.MakeSoon();
            return $"Done: {read.Count} events tidied.";
        }

        if (pending.Count == 0)
        {
            return NothingWaiting;
        }

        // With a gate, only what says something about the person is handed over; the rest is closed.
        var kept = await service.GateAsync(memory.Owner, pending, cancellationToken);
        if (kept.Count == 0)
        {
            await service.CloseConsolidationAsync(memory.Owner, pending.Select(e => e.Id).ToList(), [], cancellationToken);
            return NothingWaiting;
        }

        var page = Page(kept);
        if (page.Count == kept.Count)
        {
            return $"{ConsolidationRules.Text}\n{ConsolidationPrompt.Prompt(kept)}When you are done, call {McpConstants.Tools.Consolidate} with through={pending[^1].Id}.";
        }

        return $"{ConsolidationRules.Text}\n{ConsolidationPrompt.Prompt(page)}{kept.Count - page.Count} more events wait for a later session: tidy only these, then go on with what the person asked. " +
            $"When you are done, call {McpConstants.Tools.Consolidate} with through={page[^1].Id}.";
    }

    // A host may refuse a long tool result or set it aside where the model cannot read it: one pass hands over at most
    // MaxPendingChars of text, and always at least one event.
    private static List<MemoryEvent> Page(IReadOnlyList<MemoryEvent> kept)
    {
        var page = new List<MemoryEvent>();
        var length = 0;
        foreach (var e in kept)
        {
            length += e.Text.Length;
            if (page.Count > 0 && length > McpConstants.Limits.MaxPendingChars)
            {
                break;
            }

            page.Add(e);
        }

        return page;
    }
}
