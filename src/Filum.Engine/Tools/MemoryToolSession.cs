using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Collections.Compute;
using Filum.Engine.Memory;

namespace Filum.Engine.Tools;

// Calls may run in parallel, hence the lock.
internal sealed class MemoryToolSession(IMemoryService memory, MemoryOptions limits, ICodeSandbox sandbox, Guid userId, MemoryActor actor, IReadOnlyList<long> sources, bool consolidation)
{
    private readonly List<ToolStep> _steps = [];
    private readonly List<long> _revisions = [];
    private readonly List<string> _savedSkills = [];
    private readonly Lock _gate = new();
    private SkillProposal? _proposal;
    private int _calls;

    public IMemoryService Memory { get; } = memory;

    public MemoryOptions Limits { get; } = limits;

    public ICodeSandbox Sandbox { get; } = sandbox;

    public Guid UserId { get; } = userId;

    public MemoryActor Actor { get; } = actor;

    // The events the turn answers: the default source of what it records.
    public IReadOnlyList<long> Sources { get; } = sources;

    public bool IsConsolidation { get; } = consolidation;

    public SkillProposal? Proposal
    {
        get
        {
            lock (_gate)
            {
                return _proposal;
            }
        }
    }

    public IReadOnlyList<string> SavedSkills
    {
        get
        {
            lock (_gate)
            {
                return _savedSkills.ToList();
            }
        }
    }

    // The structured form of the last call's result, for a host that parses it; never kept in the steps.
    public System.Text.Json.JsonElement? LastStructured { get; set; }

    public IReadOnlyList<ToolStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return _steps.ToList();
            }
        }
    }

    public IReadOnlyList<long> Revisions
    {
        get
        {
            lock (_gate)
            {
                return _revisions.ToList();
            }
        }
    }

    public int NextCall()
    {
        lock (_gate)
        {
            return ++_calls;
        }
    }

    public void Record(ToolStep step)
    {
        lock (_gate)
        {
            _steps.Add(step);
        }
    }

    public void AddRevision(long revisionId)
    {
        lock (_gate)
        {
            _revisions.Add(revisionId);
        }
    }

    public void AddSavedSkill(string name)
    {
        lock (_gate)
        {
            _savedSkills.Add(name);
        }
    }

    public void Propose(SkillProposal proposal)
    {
        lock (_gate)
        {
            _proposal = proposal;
        }
    }
}
