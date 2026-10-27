using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Collections.Compute;
using Filum.Engine.Memory;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

/// <summary>
/// The agent's tools on the person's memory, built for one turn by <see cref="IMemoryToolsFactory"/>: they act for the
/// turn's user only (there is no user parameter), count their calls against the turn's limit, and record one step per
/// call with a description written here from what the tool really did. The revisions the turn created are kept so a
/// failed turn can be rolled back.
/// </summary>
public sealed class MemoryTools
{
    internal const int MaxChangeNotes = 30;

    internal const int SearchSaidMax = 5;

    private const string InvokedSkillTool = "skill";

    private readonly MemoryToolSession _session;

    // With consolidation, only the pass's tools: no new files (a proposal instead), and no change to a file the person
    // changed last.
    internal MemoryTools(IMemoryService memory, MemoryOptions limits, ICodeSandbox sandbox, Guid userId, MemoryActor actor, IReadOnlyList<long>? sources = null, bool consolidation = false)
    {
        _session = new MemoryToolSession(memory, limits, sandbox, userId, actor, sources ?? [], consolidation);
        Tools = ToolCatalog.Create(_session);
    }

    /// <summary>The last skill proposed in the turn, if any.</summary>
    public SkillProposal? Proposal => _session.Proposal;

    /// <summary>The names of the skills the turn saved.</summary>
    public IReadOnlyList<string> SavedSkills => _session.SavedSkills;

    /// <summary>The turn's tools, in the order models see them, to hand to the model.</summary>
    public IList<AITool> Tools { get; }

    /// <summary>The steps of the turn so far, in the order the calls finished.</summary>
    public IReadOnlyList<ToolStep> Steps => _session.Steps;

    /// <summary>The structured form of the last call's result, when the tool gives one (spec 040); not part of the steps.</summary>
    public System.Text.Json.JsonElement? LastStructuredContent => _session.LastStructured;

    /// <summary>The revisions the turn created, oldest first.</summary>
    public IReadOnlyList<long> Revisions => _session.Revisions;

    /// <summary>Records that the person invoked a skill by name: the code gave it to the agent, no tool was called.</summary>
    public void RecordInvoked(SkillFile skill) =>
        Record(new ToolStep(ToolStep.Read, InvokedSkillTool, skill.Path, $"Used skill /{skill.Skill.Name}", 0, null));

    /// <summary>Adds a step to the turn, in call order; a host records its own tools' calls here.</summary>
    public void Record(ToolStep step) => _session.Record(step);
}
