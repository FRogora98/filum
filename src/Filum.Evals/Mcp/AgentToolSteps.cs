using System.Text.Json;
using Filum.Evals.Checks;
using Filum.Evals.Hosts.Wire;

namespace Filum.Evals.Mcp;

// So the same expectations read them as a host's steps (spec 056): a computation, a skill used and a skill proposed as
// the host reports them.
internal static class AgentToolSteps
{
    // mcp__filum__<tool>.
    public const string FilumToolPrefix = $"mcp__{McpConfigWriter.ServerName}__";

    // A call of one is a wrote step.
    public static readonly IReadOnlySet<string> WritingTools = new HashSet<string>(StringComparer.Ordinal)
    {
        ToolNames.MemoryWrite, ToolNames.MemoryEdit, ToolNames.MemoryAppend, ToolNames.MemoryDelete, ToolNames.MemoryMove,
        ToolNames.MemorySetSensitivity, ToolNames.MemoryUndo, ToolNames.CollectionAddRows, ToolNames.CollectionUpdateRows,
        ToolNames.CollectionRemoveRows, ToolNames.CollectionAddField, ToolNames.SkillSave, ToolNames.SkillSetEnabled, ToolNames.FactRecord,
        ToolNames.FactEnd,
    };

    // A call of one is a computed step.
    public static readonly IReadOnlySet<string> ComputingTools = new HashSet<string>(StringComparer.Ordinal) { ToolNames.CollectionAggregate, ToolNames.ComputeRun };

    public static IReadOnlyList<StepDto> Of(IReadOnlyList<string> tools) => Of(tools.Select(t => new AgentToolCall(t)).ToList());

    public static IReadOnlyList<StepDto> Of(IReadOnlyList<AgentToolCall> calls) =>
        calls.Select(call =>
        {
            var tool = Bare(call.Name);
            if (tool == ToolNames.SkillUse && Text(call.Input, EvalsConstants.AgentStream.Name) is { Length: > 0 } skill)
            {
                return new StepDto(StepDto.Read, tool, null, $"{TurnRecord.UsedSkillPrefix}/{skill.TrimStart('/')}", 0, null);
            }

            var kind = WritingTools.Contains(tool) ? StepDto.Wrote : ComputingTools.Contains(tool) ? StepDto.Computed : StepDto.Read;
            return new StepDto(kind, tool, null, $"Called {tool}", 0, null);
        }).ToList();

    /// <summary>The last skill the agent proposed in the turn, as the host shows a proposal; null when it proposed none.</summary>
    public static SkillProposalDto? Proposal(IReadOnlyList<AgentToolCall> calls) =>
        calls.LastOrDefault(c => Bare(c.Name) == ToolNames.SkillPropose) is { } call && Text(call.Input, EvalsConstants.AgentStream.Name) is { Length: > 0 } name
            ? new SkillProposalDto(name, Text(call.Input, EvalsConstants.AgentStream.Description) ?? string.Empty, Text(call.Input, EvalsConstants.AgentStream.When) ?? string.Empty,
                Text(call.Input, EvalsConstants.AgentStream.Steps) ?? string.Empty)
            : null;

    private static string Bare(string name) => name.StartsWith(FilumToolPrefix, StringComparison.Ordinal) ? name[FilumToolPrefix.Length..] : name;

    private static string? Text(JsonElement? input, string field) =>
        input is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(field, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
}
