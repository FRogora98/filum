using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory.Files;

namespace Filum.Engine.Skills;

// Skills are files under /skills/ whose header the code writes.
internal sealed class SkillService(IMemoryFileStore store, MemoryWriter writer)
{
    // A file under /skills/ edited by hand into another shape is left out.
    public async Task<IReadOnlyList<SkillFile>> ListAsync(Guid userId, bool includePrivate, CancellationToken cancellationToken)
    {
        var files = (await store.ListLiveAsync(userId, SkillFormat.Folder, includePrivate, withContent: true, cancellationToken))
            .OrderByDescending(f => f.UpdatedAt);
        return files
            .Select(f => SkillFormat.Parse(f.Content).Skill is { } skill && SkillFormat.NameOf(f.Path) == skill.Name
                ? new SkillFile(skill, f.Path, f.Sensitivity, f.UpdatedAt)
                : null)
            .OfType<SkillFile>()
            .ToList();
    }

    public async Task<MemoryOutcome<SkillFile>> GetAsync(Guid userId, string? name, CancellationToken cancellationToken)
    {
        if (SkillFormat.ValidateName(name) is { } error)
        {
            return MemoryOutcome<SkillFile>.Refused(error);
        }

        var skill = (await ListAsync(userId, includePrivate: true, cancellationToken)).FirstOrDefault(s => s.Skill.Name == name);
        return skill is null ? MemoryOutcome<SkillFile>.Missing(NoSkillNamed(name)) : MemoryOutcome<SkillFile>.Ok(skill);
    }

    public Task<MemoryOutcome<MemoryChange>> SaveAsync(Guid userId, MemoryActor actor, Skill skill, bool replace, CancellationToken cancellationToken)
    {
        if (SkillFormat.Validate(skill) is { } error)
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused(error));
        }

        var path = SkillFormat.PathOf(skill.Name);
        return writer.ChangeAsync(userId, actor, path, MemoryOperation.Write, (file, _) =>
            file is not null && !replace
                ? ChangeTarget.Refuse($"There is already a skill named '{skill.Name}'; to change it, save it again with replace, or pick another name.")
                : new ChangeTarget(path, SkillFormat.Format(skill), file?.Sensitivity ?? MemorySensitivity.Normal, Deleted: false), cancellationToken);
    }

    public Task<MemoryOutcome<MemoryChange>> SetEnabledAsync(Guid userId, MemoryActor actor, string? name, bool enabled, CancellationToken cancellationToken)
    {
        if (SkillFormat.IsInvalidName(name, out var error))
        {
            return Task.FromResult(MemoryOutcome<MemoryChange>.Refused(error));
        }

        var path = SkillFormat.PathOf(name);
        return writer.ChangeAsync(userId, actor, path, MemoryOperation.Edit, (file, _) =>
        {
            if (file is null)
            {
                return ChangeTarget.NotFound(NoSkillNamed(name));
            }

            var (skill, invalid) = SkillFormat.Parse(file.Content);
            return invalid is not null || skill is null ? ChangeTarget.Refuse($"/{name} is not a valid skill: {invalid}")
                : skill.Enabled == enabled ? ChangeTarget.Refuse($"The skill /{name} is already {(enabled ? "on" : "off")}.")
                : new ChangeTarget(path, SkillFormat.Format(skill with { Enabled = enabled }), file.Sensitivity, Deleted: false);
        }, cancellationToken);
    }

    private static string NoSkillNamed(string? name) => $"There is no skill named '{name}'.";
}
