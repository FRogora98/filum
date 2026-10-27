using Filum.Engine.Memory.Contracts;
using Filum.Evals.Checks;
using Filum.Evals.Hosts.Wire;

namespace Filum.Evals.Mcp;

// Read with the engine, as a host would show it, with the rules it keeps as facts (spec 056).
internal static class FilumMemoryReader
{
    // No memory was made when it is missing.
    private const string EngineFolder = ".filum";

    private const string AllFiles = "/";

    private const string Author = "agent";

    public static async Task<MemorySnapshot> ReadAsync(string home, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(home, EngineFolder)))
        {
            return new MemorySnapshot([]);
        }

        var store = new LocalFolderStore(home);
        using var engine = FilumEngine.Create(store);
        IMemoryReader memory = engine.Memory;
        var files = (await memory.ListAsync(store.Owner, AllFiles, includePrivate: true, cancellationToken)).Value ?? [];
        var details = new List<MemoryFileDetailDto>();
        foreach (var info in files)
        {
            if ((await memory.GetFileAsync(store.Owner, info.Path, cancellationToken)).Value is { } file)
            {
                var kind = MemoryPaths.KindOf(info.Path) == MemoryFileKind.Collection ? MemoryFileDto.CollectionKind : MemoryFileDto.DocumentKind;
                details.Add(new MemoryFileDetailDto(info.Path, kind, info.SizeBytes, info.LineCount, info.Sensitivity, info.UpdatedAt, info.Header, info.RowCount,
                    file.Content, new MemoryOriginDto(Author, null, info.UpdatedAt)));
            }
        }

        return new MemorySnapshot(details, Rules: await engine.Memory.RulesAsync(store.Owner, null, cancellationToken));
    }
}
