using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Skills;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// A person's skills: the procedures they keep under /skills/. A role of <see cref="IMemoryService"/>, for callers that
/// need only this.
/// </summary>
public interface IMemorySkills
{
    /// <summary>The person's skills, most recently changed first; a file under /skills/ that is not a valid skill is left out.</summary>
    Task<IReadOnlyList<SkillFile>> ListSkillsAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken);

    /// <summary>One skill by name, private ones included.</summary>
    Task<MemoryOutcome<SkillFile>> GetSkillAsync(Guid userId, string? name, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a skill; the code writes its header. A name another skill already has is refused unless
    /// <paramref name="replace"/>: then the skill is changed, keeping its sensitivity.
    /// </summary>
    Task<MemoryOutcome<MemoryChange>> SaveSkillAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken);

    /// <summary>Turns a skill on or off by rewriting its header; its steps stay as they are.</summary>
    Task<MemoryOutcome<MemoryChange>> SetSkillEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken);
}
