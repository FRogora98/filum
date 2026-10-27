using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Instructions;
using Filum.Engine.Memory.Files;
using Filum.Engine.Packs;
using Filum.Engine.Skills;
using Microsoft.Extensions.Logging;

namespace Filum.Engine.Memory.Core;

// Everything a new memory starts with is written by the platform.
internal sealed class CoreBootstrapper(IMemoryFileStore store, MemoryWriter writer, SkillService skills, EnginePack engine, ILogger<CoreBootstrapper> logger)
{
    private const string NoPackage = "no package";

    private Pack? Pack => engine.Pack;

    public async Task<string> EnsureCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken) is { } existing)
        {
            return existing.Content;
        }

        var problems = await CreateMemoryAsync(userId, cancellationToken);
        if (problems.Count > 0)
        {
            logger.PackProblems(Pack?.Name, problems.Count, userId);
        }

        var core = await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken)
            ?? throw new InvalidOperationException($"The core could not be created: {string.Join("; ", problems)}");
        return core.Content;
    }

    // PackLoader runs it on an empty memory to check a package with these very rules.
    public async Task<IReadOnlyList<string>> CreateMemoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var core = MemoryText.Normalize(Pack?.Core ?? PlatformInstructions.CoreTemplate);
        var created = await writer.ChangeAsync(userId, MemoryActor.Platform, MemoryPaths.CorePath, MemoryOperation.CreateCore, (file, _) =>
            file is not null ? null : new ChangeTarget(MemoryPaths.CorePath, core, MemorySensitivity.Normal, Deleted: false), cancellationToken);
        if (created.IsRefused)
        {
            if (await store.FindLiveAsync(userId, MemoryPaths.CorePath, cancellationToken) is not null)
            {
                // Another request created it first: that one made the rest too.
                logger.CoreCreatedConcurrently(userId);
                return [];
            }

            return [$"{MemoryPaths.CorePath}: {created.Refusal}"];
        }

        await EnsureLearnedAsync(userId, cancellationToken);
        var problems = new List<string>();
        problems.AddRange(await WritePackFilesAsync(userId, cancellationToken));
        problems.AddRange(await SaveSkillsAsync(userId, cancellationToken));
        logger.MemoryCreated(userId, Pack is null ? NoPackage : $"package {Pack.Name} {Pack.Version}");
        return problems;
    }

    public async Task<string> EnsureLearnedAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await store.FindLiveAsync(userId, LearnedFile.Path, cancellationToken) is { } existing)
        {
            return LearnedFile.ForInstructions(existing.Content);
        }

        await writer.ChangeAsync(userId, MemoryActor.Platform, LearnedFile.Path, MemoryOperation.Write, (file, _) =>
            file is not null ? null : new ChangeTarget(LearnedFile.Path, MemoryText.Normalize(LearnedFile.Template), MemorySensitivity.Normal, Deleted: false), cancellationToken);
        return LearnedFile.ForInstructions(LearnedFile.Template);
    }

    private async Task<IReadOnlyList<string>> WritePackFilesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        foreach (var file in Pack?.Files ?? [])
        {
            var written = await writer.ChangeAsync(userId, MemoryActor.Platform, file.Path, MemoryOperation.Write, (_, _) =>
                new ChangeTarget(file.Path, MemoryText.Normalize(file.Content), file.Sensitivity, Deleted: false), cancellationToken);
            if (written.IsRefused)
            {
                problems.Add($"{file.Path}: {written.Refusal}");
            }
        }

        return problems;
    }

    // The starter skills the package does not replace, then the package's own.
    private async Task<IReadOnlyList<string>> SaveSkillsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var packSkills = Pack?.Skills ?? [];
        foreach (var skill in StarterSkills.All.Where(s => packSkills.All(p => p.Name != s.Name)).Concat(packSkills))
        {
            var saved = await skills.SaveAsync(userId, MemoryActor.Platform, skill, replace: false, cancellationToken);
            if (saved.IsRefused)
            {
                problems.Add($"{SkillFormat.PathOf(skill.Name)}: {saved.Refusal}");
            }
        }

        return problems;
    }
}
