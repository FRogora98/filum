using Filum.Evals.Hosts.Wire;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Checks;

// A file of the person's is found by any word of its path.
internal static class MemoryExpectations
{
    private const string RowSeparator = " | ";

    public static (bool, string) File(Expectation e, MemorySnapshot memory)
    {
        var candidates = Named(e, memory).Where(f => e.Kind is null || f.Kind == e.Kind).ToList();
        if (candidates.Count == 0)
        {
            return (false, $"no {e.Kind ?? "file"} named {string.Join("|", e.NameAny)} among {Names(memory)}");
        }

        return e.Sensitivity is null || candidates.Any(f => f.Sensitivity == e.Sensitivity)
            ? (true, candidates[0].Path)
            : (false, $"{candidates[0].Path} is {candidates[0].Sensitivity}");
    }

    public static (bool, string) Rows(Expectation e, MemorySnapshot memory)
    {
        var collection = Named(e, memory).FirstOrDefault(f => f.Kind == MemoryFileDto.CollectionKind);
        if (collection is null)
        {
            return (false, $"no collection named {string.Join("|", e.NameAny)} among {Names(memory)}");
        }

        var rows = Csv.Check(collection.Content).Rows.Select(r => string.Join(RowSeparator, r.Fields)).ToList();
        if (e.Count is { } count && rows.Count != count)
        {
            return (false, $"{collection.Path} has {rows.Count} rows, expected {count}");
        }

        var missing = e.Contain.Where(value => !rows.Any(row => ComparableText.Contains(row, value))).ToList();
        return missing.Count == 0
            ? (true, $"{collection.Path}: {rows.Count} rows")
            : (false, $"{collection.Path} has no row with {string.Join(", ", missing)}");
    }

    // The model picks names and folders: any word anywhere in the path counts. Skill files are checked by the skill
    // expectations.
    private static IEnumerable<MemoryFileDetailDto> Named(Expectation e, MemorySnapshot memory) =>
        memory.DataFiles.Where(f => e.NameAny.Count == 0 || e.NameAny.Any(word => ComparableText.Contains(f.Path, word)));

    private static string Names(MemorySnapshot memory)
    {
        var names = memory.DataFiles.Select(f => f.Path).ToList();
        return names.Count == 0 ? "(no files but the core and the skills)" : string.Join(", ", names);
    }
}
