using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.Tools;

/// <summary>
/// What a tool does to the memory, for a host that gates or explains calls (MCP's tool annotations, spec 040): whether it
/// only reads, whether it can remove what was there, and whether calling it twice with the same arguments changes
/// nothing more. No tool reaches outside the memory.
/// </summary>
/// <param name="ReadOnly">It changes nothing.</param>
/// <param name="Destructive">It can remove or undo what the memory held (the history keeps it).</param>
/// <param name="Idempotent">The same call again changes nothing more.</param>
public sealed record ToolTraits(bool ReadOnly, bool Destructive, bool Idempotent)
{
    private static readonly ToolTraits Reads = new(true, false, true);
    private static readonly ToolTraits Writes = new(false, false, false);
    private static readonly ToolTraits WritesTheSame = new(false, false, true);
    private static readonly ToolTraits Removes = new(false, true, false);

    private static readonly Dictionary<string, ToolTraits> ByTool = new(StringComparer.Ordinal)
    {
        [ToolNames.MemoryOverview] = Reads,
        [ToolNames.MemoryRead] = Reads,
        [ToolNames.MemorySearch] = Reads,
        [ToolNames.MemoryHistory] = Reads,
        [ToolNames.EventsSearch] = Reads,
        [ToolNames.EventsRead] = Reads,
        [ToolNames.FactsCurrent] = Reads,
        [ToolNames.FactsHistory] = Reads,
        [ToolNames.CollectionAggregate] = Reads,
        [ToolNames.CollectionTrend] = Reads,
        [ToolNames.ComputeRun] = Reads,
        [ToolNames.SkillList] = Reads,
        [ToolNames.SkillUse] = Reads,
        [ToolNames.MemoryWrite] = WritesTheSame,
        [ToolNames.MemorySetSensitivity] = WritesTheSame,
        [ToolNames.SkillSetEnabled] = WritesTheSame,
        [ToolNames.MemoryEdit] = Writes,
        [ToolNames.MemoryAppend] = Writes,
        [ToolNames.MemoryMove] = Writes,
        [ToolNames.MemoryPropose] = Writes,
        [ToolNames.FactRecord] = Writes,
        [ToolNames.FactEnd] = Writes,
        [ToolNames.ProposalAnswer] = Writes,
        [ToolNames.CollectionAddRows] = Writes,
        [ToolNames.CollectionUpdateRows] = Writes,
        [ToolNames.CollectionAddField] = Writes,
        [ToolNames.SkillSave] = Writes,
        [ToolNames.SkillPropose] = Writes,
        [ToolNames.MemoryDelete] = Removes,
        [ToolNames.MemoryUndo] = Removes,
        [ToolNames.CollectionRemoveRows] = Removes,
    };

    /// <summary>The traits of an engine tool; null for a name the engine does not have.</summary>
    public static ToolTraits? Of(string tool) => ByTool.GetValueOrDefault(tool);
}
