using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Collections;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Memory.Files;

internal sealed class MemoryFileReader(IMemoryFileStore store, IOptions<MemoryOptions> options)
{
    private MemoryOptions Limits => options.Value;

    public async Task<MemoryOutcome<IReadOnlyList<MemoryFileInfo>>> ListAsync(Guid userId, string? prefix, bool includePrivate, CancellationToken cancellationToken)
    {
        if (MemoryPaths.ValidatePrefix(prefix) is { } error)
        {
            return MemoryOutcome<IReadOnlyList<MemoryFileInfo>>.Refused(error);
        }

        var files = (await store.ListLiveAsync(userId, prefix, includePrivate, withContent: false, cancellationToken))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(Info)
            .ToList();
        return MemoryOutcome<IReadOnlyList<MemoryFileInfo>>.Ok(files);
    }

    public async Task<MemoryOutcome<MemoryReadResult>> ReadAsync(Guid userId, string? path, int? fromLine, int? toLine, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<MemoryReadResult>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path, cancellationToken);
        if (file is null)
        {
            return MemoryOutcome<MemoryReadResult>.Missing(FileRefusals.NoFileAt(path));
        }

        var lines = MemoryText.LinesOf(file.Content);
        var from = Math.Max(1, fromLine ?? 1);
        if (lines.Count > 0 && from > lines.Count)
        {
            return MemoryOutcome<MemoryReadResult>.Refused($"{path} has {lines.Count} lines; start at a line between 1 and {lines.Count}.");
        }

        var to = Math.Min(lines.Count, Math.Min(toLine ?? int.MaxValue, from + Limits.MaxReadLines - 1));
        var slice = to < from ? [] : lines.Skip(from - 1).Take(to - from + 1).ToList();
        return MemoryOutcome<MemoryReadResult>.Ok(new MemoryReadResult(path, file.Sensitivity, from, Math.Max(to, from - 1), lines.Count, slice));
    }

    public async Task<MemoryOutcome<MemoryFileContent>> GetFileAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<MemoryFileContent>.Refused(error);
        }

        var file = await store.FindLiveAsync(userId, path, cancellationToken);
        return file is null
            ? MemoryOutcome<MemoryFileContent>.Missing(FileRefusals.NoFileAt(path))
            : MemoryOutcome<MemoryFileContent>.Ok(new MemoryFileContent(Info(file), file.Content));
    }

    public async Task<MemoryOutcome<CsvTable>> ReadCollectionAsync(Guid userId, string? path, CancellationToken cancellationToken)
    {
        if (MemoryPaths.IsInvalid(path, out var error))
        {
            return MemoryOutcome<CsvTable>.Refused(error);
        }

        if (MemoryPaths.KindOf(path) != MemoryFileKind.Collection)
        {
            return MemoryOutcome<CsvTable>.Refused($"{path} is a document; only collections (.csv) can be computed on.");
        }

        var content = (await store.FindLiveAsync(userId, path, cancellationToken))?.Content;
        return content is null
            ? MemoryOutcome<CsvTable>.Missing(FileRefusals.NoFileAt(path))
            : MemoryOutcome<CsvTable>.Ok(Csv.Check(content));
    }

    public async Task<MemoryOutcome<MemorySearchResult>> SearchAsync(Guid userId, string? query, string? prefix, bool includePrivate, CancellationToken cancellationToken)
    {
        if (!FileRefusals.IsValidQuery(query))
        {
            return MemoryOutcome<MemorySearchResult>.Refused(FileRefusals.QueryLength);
        }

        if (MemoryPaths.ValidatePrefix(prefix) is { } error)
        {
            return MemoryOutcome<MemorySearchResult>.Refused(error);
        }

        var literal = (await store.SearchAsync(userId, query, prefix, includePrivate, cancellationToken))
            .OrderByDescending(f => f.UpdatedAt)
            .ToList();
        var hits = new List<MemorySearchHit>();
        if (Collect(hits, literal, line => line.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return MemoryOutcome<MemorySearchResult>.Ok(new MemorySearchResult(hits, Truncated: true));
        }

        // Then the lines that hold every word of the query, in another order or form ("Okafor River born city" finds
        // "River Okafor: born in Lisbon, two cities later"): the text as written stays first.
        var words = MemoryText.QueryWordsOf(query!);
        if (words.Count > 0)
        {
            var all = (await store.ListLiveAsync(userId, prefix, includePrivate, withContent: true, cancellationToken))
                .OrderByDescending(f => f.UpdatedAt)
                .ToList();
            var seen = hits.Select(h => (h.Path, h.Line)).ToHashSet();
            var truncated = Collect(hits, all, (line, path, number) => !seen.Contains((path, number)) && words.IsSubsetOf(MemoryText.WordsOf(line)));
            return MemoryOutcome<MemorySearchResult>.Ok(new MemorySearchResult(hits, truncated));
        }

        return MemoryOutcome<MemorySearchResult>.Ok(new MemorySearchResult(hits, Truncated: false));
    }

    // True when the limit was reached with more lines left.
    private bool Collect(List<MemorySearchHit> hits, IReadOnlyList<StoredFile> files, Func<string, bool> matches) =>
        Collect(hits, files, (line, _, _) => matches(line));

    private bool Collect(List<MemorySearchHit> hits, IReadOnlyList<StoredFile> files, Func<string, string, int, bool> matches)
    {
        foreach (var file in files)
        {
            var lines = MemoryText.LinesOf(file.Content);
            for (var i = 0; i < lines.Count; i++)
            {
                if (!matches(lines[i], file.Path, i + 1))
                {
                    continue;
                }

                if (hits.Count == Limits.MaxSearchResults)
                {
                    return true;
                }

                hits.Add(new MemorySearchHit(file.Path, i + 1, lines[i]));
            }
        }

        return false;
    }

    private static MemoryFileInfo Info(StoredFile f) => new(f.Path, f.SizeBytes, f.LineCount, f.Sensitivity, f.UpdatedAt, f.Header, f.RowCount);
}
