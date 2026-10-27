using Filum.Evals.Hosts.Wire;

namespace Filum.Evals.Checks;

// Available is false when the host maps no memory group: the memory expectations cannot be checked then. Rules are the
// ones a memory keeps as facts (filum-mcp, spec 056); a host's are in its core.
internal sealed record MemorySnapshot(IReadOnlyList<MemoryFileDetailDto> Files, bool Available = true, string Rules = "")
{
    public static MemorySnapshot Unavailable { get; } = new([], Available: false);

    public string Core => Files.FirstOrDefault(f => f.Path == MemoryPaths.CorePath)?.Content ?? string.Empty;

    // Where a rule the person set is kept: the core, or the rules that hold as facts.
    public string CoreAndRules => Rules.Length == 0 ? Core : $"{Core}\n{Rules}".Trim();

    // A skill file that does not parse is left out.
    public IReadOnlyList<(string Path, Skill Skill)> OwnSkills =>
        Files.Where(f => SkillFormat.IsSkillPath(f.Path))
            .SelectMany<MemoryFileDetailDto, (string Path, Skill Skill)>(f =>
                SkillFormat.Parse(f.Content).Skill is { } skill && !StarterSkills.All.Any(starter => starter.Name == skill.Name)
                    ? [(f.Path, skill)]
                    : [])
            .ToList();

    public IEnumerable<MemoryFileDetailDto> DataFiles => Files.Where(f => f.Path != MemoryPaths.CorePath && !SkillFormat.IsSkillPath(f.Path));
}
