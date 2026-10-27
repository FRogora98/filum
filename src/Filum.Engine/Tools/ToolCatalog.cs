using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Consolidation;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

internal static class ToolCatalog
{
    // Part of what models see: a change here changes the catalog snapshot. Written in full, not derived from the groups
    // (docs/EXCEPTIONS.md, SVC-04).
    internal static readonly IReadOnlyList<string> Order =
    [
        ToolNames.MemoryOverview, ToolNames.MemoryRead, ToolNames.MemorySearch, ToolNames.EventsSearch, ToolNames.EventsRead,
        ToolNames.FactRecord, ToolNames.FactEnd, ToolNames.FactsCurrent, ToolNames.FactsHistory, ToolNames.ProposalAnswer, ToolNames.MemoryPropose,
        ToolNames.MemoryWrite, ToolNames.MemoryEdit, ToolNames.MemoryAppend,
        ToolNames.CollectionAddRows, ToolNames.CollectionUpdateRows, ToolNames.CollectionRemoveRows, ToolNames.CollectionAddField,
        ToolNames.MemoryDelete, ToolNames.MemoryMove, ToolNames.MemorySetSensitivity, ToolNames.MemoryHistory, ToolNames.MemoryUndo,
        ToolNames.CollectionAggregate, ToolNames.ComputeRun, ToolNames.CollectionTrend,
        ToolNames.SkillList, ToolNames.SkillUse, ToolNames.SkillSave, ToolNames.SkillPropose, ToolNames.SkillSetEnabled,
    ];

    // A consolidation pass gets only its tools, and only a pass may propose.
    public static IList<AITool> Create(MemoryToolSession session)
    {
        var byName = Groups(session).SelectMany(g => g.Functions()).ToDictionary(f => f.Name, StringComparer.Ordinal);
        if (byName.Count != Order.Count)
        {
            throw new InvalidOperationException($"Every tool needs its place in the catalog's order: {string.Join(", ", byName.Keys.Except(Order))}.");
        }

        var excluded = new HashSet<string>(session.Limits.ExcludedTools, StringComparer.Ordinal);
        var consolidation = session.IsConsolidation;
        return Order
            .Select(name => byName[name])
            .Where(t => !excluded.Contains(t.Name) && (consolidation ? ConsolidationPrompt.Tools.Contains(t.Name) : t.Name != ConsolidationPrompt.ProposeTool))
            .Cast<AITool>()
            .ToList();
    }

    internal static IReadOnlyList<MemoryToolGroup> Groups(MemoryToolSession session) =>
    [
        new FileReadTools(session), new FileWriteTools(session), new EventTools(session), new FactTools(session),
        new ProposalTools(session), new CollectionTools(session), new HistoryTools(session), new ComputeTools(session),
        new SkillTools(session),
    ];
}
