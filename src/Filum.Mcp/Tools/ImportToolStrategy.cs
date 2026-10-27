using System.ComponentModel;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Memory.Contracts;
using Filum.Mcp.Session;
using Filum.Mcp.Search;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.Tools;

internal sealed class ImportToolStrategy(McpEngines engines, OpenedMemory memory, VectorMaker vectors) : IServerToolStrategy
{
    private const string Description = "Import a file the person has (a WhatsApp chat export, an mbox mailbox, an ICS calendar, a CSV) into what was said, each item with its own date. Preview first and show it to the person; import only when they agree.";

    public AIFunction Function() => AIFunctionFactory.Create(ImportAsync, McpConstants.Tools.Import, Description);

    public async Task<string> ImportAsync(
        [Description("The file's path on this machine.")] string path,
        [Description("whatsapp, mbox, ics or csv; detected when not given.")] string? format = null,
        [Description("normal, sensitive or private, for every item.")] string sensitivity = MemorySensitivity.Normal,
        [Description("True to only see what would be imported; false to import.")] bool preview = true,
        CancellationToken cancellationToken = default)
    {
        var content = await ReadAsync(path, cancellationToken);
        IMemoryImports imports = engines.Current.Memory;
        var outcome = await imports.ImportAsync(memory.Owner, new ImportRequest(Path.GetFileName(path), content, format, sensitivity, preview), cancellationToken);
        if (outcome.IsRefused)
        {
            throw new McpException($"Refused: {outcome.Refusal}");
        }

        var result = outcome.Value;
        if (result.Written)
        {
            _ = vectors.MakeSoon();
        }

        return Report(result, preview);
    }

    private static async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new McpException($"Refused: {path} cannot be read ({e.GetType().Name}).");
        }
    }

    private static string Report(ImportResult result, bool preview)
    {
        var counts = $"{result.Format}: {result.Items} items, {result.New} new, {result.Duplicates} already imported, {result.Skipped} lines skipped.";
        var sample = result.Sample.Count == 0
            ? string.Empty
            : "\nThe first new items:\n" + string.Join("\n", result.Sample.Select(t => "- " + ToolText.Cut(t.Replace('\n', ' '), McpConstants.Limits.SampleLength)));
        return preview
            ? $"Preview, nothing written. {counts}{sample}\nShow this to the person; import with preview false only when they agree."
            : $"{(result.Written ? $"Imported {result.New} items" : "Nothing new to import")}. {counts}\nThen tidy them into the memory with {McpConstants.Tools.Consolidate}.";
    }
}
