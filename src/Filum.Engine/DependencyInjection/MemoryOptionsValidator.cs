using Filum.Engine.Abstractions.Options;
using Filum.Engine.Tools;
using Microsoft.Extensions.Options;

namespace Filum.Engine.DependencyInjection;

// Every limit above zero, every excluded tool a real one.
internal sealed class MemoryOptionsValidator : IValidateOptions<MemoryOptions>
{
    public ValidateOptionsResult Validate(string? name, MemoryOptions options)
    {
        var limits = new Dictionary<string, int>
        {
            [nameof(MemoryOptions.MaxFileBytes)] = options.MaxFileBytes,
            [nameof(MemoryOptions.MaxCollectionBytes)] = options.MaxCollectionBytes,
            [nameof(MemoryOptions.MaxFilesPerUser)] = options.MaxFilesPerUser,
            [nameof(MemoryOptions.MaxCoreChars)] = options.MaxCoreChars,
            [nameof(MemoryOptions.MaxReadLines)] = options.MaxReadLines,
            [nameof(MemoryOptions.MaxSearchResults)] = options.MaxSearchResults,
            [nameof(MemoryOptions.MaxToolCallsPerTurn)] = options.MaxToolCallsPerTurn,
            [nameof(MemoryOptions.IndexMaxFiles)] = options.IndexMaxFiles,
            [nameof(MemoryOptions.IndexMaxChars)] = options.IndexMaxChars,
            [nameof(MemoryOptions.SkillListMax)] = options.SkillListMax,
            [nameof(MemoryOptions.MaxSkillChars)] = options.MaxSkillChars,
            [nameof(MemoryOptions.MaxHistoryChars)] = options.MaxHistoryChars,
            [nameof(MemoryOptions.MaxImportItems)] = options.MaxImportItems,
            [nameof(MemoryOptions.MaxImportBytes)] = options.MaxImportBytes,
            [nameof(MemoryOptions.VectorsPerSearch)] = options.VectorsPerSearch,
            [nameof(MemoryOptions.EventsSearchMax)] = options.EventsSearchMax,
            [nameof(MemoryOptions.RecallSessionsMax)] = options.RecallSessionsMax,
            [nameof(MemoryOptions.RecallBudgetChars)] = options.RecallBudgetChars,
            [nameof(MemoryOptions.RerankDepth)] = options.RerankDepth,
            [nameof(MemoryOptions.BriefChars)] = options.BriefChars,
            [nameof(MemoryOptions.RecallChars)] = options.RecallChars,
        };
        var failures = limits
            .Where(l => l.Value <= 0)
            .Select(l => $"{MemoryOptions.SectionName}:{l.Key} must be above zero (it is {l.Value}).")
            .ToList();
        if (options.ExcludedTools is null)
        {
            failures.Add($"{MemoryOptions.SectionName}:{nameof(MemoryOptions.ExcludedTools)} must be a list, empty when no tool is left out.");
        }
        else
        {
            failures.AddRange(options.ExcludedTools
                .Where(t => !ToolCatalog.Order.Contains(t))
                .Select(t => $"{MemoryOptions.SectionName}:{nameof(MemoryOptions.ExcludedTools)} names '{t}', which is not a tool of the engine."));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
