using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Collections;
using Filum.Engine.Memory.Files;
using Filum.Engine.Skills;

namespace Filum.Engine.Memory.History;

// By comparing a revision with the one before.
internal static class RevisionSummary
{
    public static string Of(StoredRevision revision, StoredRevision? previous)
    {
        var collection = MemoryPaths.KindOf(revision.Path) == MemoryFileKind.Collection;
        int Units(StoredRevision? r) => r is null || r.Deleted ? 0
            : collection ? Csv.Check(r.Content) is { IsValid: true } table ? table.Rows.Count : 0
            : MemoryText.LinesOf(r.Content).Count;
        string Count(int n) => n == 1 ? $"1 {(collection ? "row" : "line")}" : $"{n} {(collection ? "rows" : "lines")}";

        return revision.Operation switch
        {
            MemoryOperation.CreateCore => "Created",
            MemoryOperation.Write => previous is null || previous.Deleted ? "Created" : "Rewrote",
            MemoryOperation.Edit when TurnedOnOrOff(revision, previous) is { } enabled => enabled ? "Turned on" : "Turned off",
            MemoryOperation.Edit when collection && previous is not null && !previous.Deleted => CollectionEdit(revision, previous, Units(revision) - Units(previous)),
            MemoryOperation.Edit => "Edited",
            MemoryOperation.Append when previous is null || previous.Deleted => $"Created with {Count(Units(revision))}",
            MemoryOperation.Append => $"Added {Count(Math.Max(0, Units(revision) - Units(previous)))}",
            MemoryOperation.Delete => "Deleted",
            MemoryOperation.Move => previous is null ? "Moved" : $"Moved from {previous.Path}",
            MemoryOperation.Sensitivity => $"Marked {revision.Sensitivity}",
            MemoryOperation.Undo => "Undid a change",
            MemoryOperation.Rollback => "Took back the changes of a failed answer",
            MemoryOperation.Restore => "Restored an earlier version",
            MemoryOperation.Outside => revision.Deleted ? "Deleted outside Filum" : previous is null || previous.Deleted ? "Created outside Filum" : "Changed outside Filum",
            _ => revision.Operation
        };
    }

    // Null when the edit did neither.
    private static bool? TurnedOnOrOff(StoredRevision revision, StoredRevision? previous) =>
        SkillFormat.IsSkillPath(revision.Path) && previous is not null && !previous.Deleted
            && SkillFormat.Parse(revision.Content).Skill is { } now && SkillFormat.Parse(previous.Content).Skill is { } before && now.Enabled != before.Enabled
            ? now.Enabled
            : null;

    private static string CollectionEdit(StoredRevision revision, StoredRevision previous, int rowsDelta)
    {
        static string Header(string content) => content.Split('\n', 2)[0].Trim();
        static string Rows(int n) => n == 1 ? "1 row" : $"{n} rows";
        return rowsDelta < 0 ? $"Removed {Rows(-rowsDelta)}"
            : rowsDelta > 0 ? $"Added {Rows(rowsDelta)}"
            : Header(revision.Content) != Header(previous.Content) ? "Changed the fields"
            : "Changed rows";
    }
}
