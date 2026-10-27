using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Facts;
using Filum.Engine.Memory.Core;
using Filum.Engine.Skills;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Memory.Files;

// Built from the files themselves at every message, so the agent does not need to look around before it acts.
internal sealed class MemoryIndexBuilder(IMemoryFileStore store, IOptions<MemoryOptions> options)
{
    private const string NothingYet = "(nothing yet besides the core)";
    private const int SummaryMaxChars = 100;

    private MemoryOptions Limits => options.Value;

    public async Task<string> BuildAsync(Guid userId, CancellationToken cancellationToken)
    {
        var files = (await store.ListLiveAsync(userId, "/", includePrivate: false, withContent: true, cancellationToken))
            .Where(f => f.Path != MemoryPaths.CorePath && f.Path != LearnedFile.Path && !SkillFormat.IsSkillPath(f.Path))
            .OrderByDescending(f => f.UpdatedAt)
            .ToList();
        if (files.Count == 0)
        {
            return NothingYet;
        }

        var lines = new List<string>();
        var length = 0;
        foreach (var line in files.Select(Line))
        {
            if (lines.Count == Limits.IndexMaxFiles || length + line.Length > Limits.IndexMaxChars)
            {
                break;
            }

            lines.Add(line);
            length += line.Length + 1;
        }

        if (lines.Count < files.Count)
        {
            lines.Add($"- …and {files.Count - lines.Count} older files not listed here: memory_search finds them.");
        }

        return string.Join('\n', lines);
    }

    private static string Line(StoredFile file) =>
        file.Path == FactTable.Path
            ? $"- {file.Path} (facts that hold for a time · {file.RowCount} rows · changed {file.UpdatedAt:yyyy-MM-dd}): use facts_current and facts_history"
            : file.Header is null
                ? $"- {file.Path} (document · {file.LineCount} lines · changed {file.UpdatedAt:yyyy-MM-dd}){Summary(file.Content)}"
                : $"- {file.Path} (collection · fields: {file.Header.Replace(",", ", ")} · {file.RowCount} rows · changed {file.UpdatedAt:yyyy-MM-dd})";

    // Its first line of text, or its title when it has only headings.
    private static string Summary(string content)
    {
        var lines = MemoryText.LinesOf(content).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var text = lines.FirstOrDefault(l => !l.StartsWith('#')) ?? lines.FirstOrDefault()?.TrimStart('#').Trim();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return ": " + (text.Length > SummaryMaxChars ? text[..SummaryMaxChars] + "…" : text);
    }
}
