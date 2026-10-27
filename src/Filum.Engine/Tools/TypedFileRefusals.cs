using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Facts;
using Filum.Engine.Skills;

namespace Filum.Engine.Tools;

// Facts, collections and skills are written only through their own tools, which keep them valid.
internal static class TypedFileRefusals
{
    private const string UseFactTools = $"keeps facts that hold for a time: use {ToolNames.FactRecord}; the platform closes the old value and writes the file.";

    private const string UseCollectionTools = $"is a collection: use {ToolNames.CollectionAddRows}, {ToolNames.CollectionUpdateRows} or {ToolNames.CollectionRemoveRows}; the platform writes the file.";

    private const string UseSkillTools = $"is a skill: use {ToolNames.SkillSave} (with replace to change it) or {ToolNames.SkillSetEnabled}; the platform writes the file.";

    public static Task<MemoryOutcome<MemoryChange>>? FactsOnly(string path) =>
        path == FactTable.Path ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseFactTools}")) : null;

    public static Task<MemoryOutcome<MemoryChange>>? RefuseTyped(string path) =>
        path == FactTable.Path ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseFactTools}")) :
        SkillFormat.IsSkillPath(path) ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseSkillTools}"))
        : IsCollection(path) ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"{path} {UseCollectionTools}"))
        : null;

    private static bool IsCollection(string? path) => path?.EndsWith(EngineConstants.FileExtensions.Collection, StringComparison.Ordinal) == true;
}
