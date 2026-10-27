using System.ComponentModel;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Consolidation;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Memory;
using Microsoft.Extensions.AI;
using static Filum.Engine.Tools.ToolText;
using static Filum.Engine.Tools.TypedFileRefusals;

namespace Filum.Engine.Tools;

internal sealed class FileWriteTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryWriter Writer => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(Write, ToolNames.MemoryWrite,
            "Create a document (.md) or replace its whole content."),
        AIFunctionFactory.Create(Edit, ToolNames.MemoryEdit,
            "Replace one exact occurrence of oldText with newText in a document."),
        AIFunctionFactory.Create(Append, ToolNames.MemoryAppend,
            "Add text at the end of a document, creating it if missing."),
        AIFunctionFactory.Create(Delete, ToolNames.MemoryDelete,
            "Delete a file; it stays in the history and can be undone. Not the core."),
        AIFunctionFactory.Create(Move, ToolNames.MemoryMove,
            "Rename or move a file, keeping its history."),
        AIFunctionFactory.Create(SetSensitivity, ToolNames.MemorySetSensitivity,
            "Set a file's sensitivity: normal, sensitive or private."),
    ];

    private Task<string> Write(
        [Description("Path of the document, ending in .md.")] string path,
        [Description("The whole content of the file.")] string content,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemoryWrite, path, "write", () => RefuseTyped(path) ?? Writer.WriteAsync(UserId, Actor, path, content, cancellationToken),
            c => c.Created ? $"Created {path}" : $"Rewrote {path}",
            c => $"{(c.Created ? "Created" : "Replaced")} {path} ({Plural(c.LineCount, "line")}).", cancellationToken);

    private Task<string> Edit(
        [Description("Path of the file.")] string path,
        [Description("Exact text to replace, appearing once.")] string oldText,
        [Description("Its replacement.")] string newText,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemoryEdit, path, "edit", () => RefuseTyped(path) ?? NewCoreSection(path, oldText, newText) ?? Writer.EditAsync(UserId, Actor, path, oldText, newText, cancellationToken),
            _ => $"Edited {path}",
            _ => $"Edited {path}.", cancellationToken);

    private Task<string> Append(
        [Description("Path of the file.")] string path,
        [Description("Text to add.")] string text,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemoryAppend, path, "add to", () => RefuseTyped(path) ?? NewCoreSection(path, null, text) ?? Writer.AppendAsync(UserId, Actor, path, text, cancellationToken),
            c => AddedTo(c, path),
            c => AddedTo(c, path) + ".", cancellationToken);

    private Task<string> Delete(
        [Description("Path of the file.")] string path,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemoryDelete, path, "delete", () => Writer.DeleteAsync(UserId, Actor, path, cancellationToken),
            _ => $"Deleted {path}",
            _ => $"Deleted {path}; it stays in the history.", cancellationToken);

    private Task<string> Move(
        [Description("Current path of the file.")] string from,
        [Description("New path of the file.")] string to,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemoryMove, from, "move", () => Writer.MoveAsync(UserId, Actor, from, to, cancellationToken),
            _ => $"Moved {from} to {to}",
            _ => $"Moved {from} to {to}.",
            cancellationToken, stepPath: to);

    private Task<string> SetSensitivity(
        [Description("Path of the file.")] string path,
        [Description("normal, sensitive or private.")] string level,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.MemorySetSensitivity, path, "change the sensitivity of", () => Writer.SetSensitivityAsync(UserId, Actor, path, level, cancellationToken),
            _ => $"Marked {path} {level}",
            _ => $"{path} is now {level}.", cancellationToken);

    // A pass adds to the core's sections, and proposes a new one.
    private Task<MemoryOutcome<MemoryChange>>? NewCoreSection(string path, string? oldText, string? newText)
    {
        static int Headings(string? text) => (text ?? string.Empty).Split('\n').Count(l => l.StartsWith("# ", StringComparison.Ordinal));
        return Session.IsConsolidation && MemoryPaths.IsCore(path) && Headings(newText) > Headings(oldText)
            ? Task.FromResult(MemoryOutcome<MemoryChange>.Refused($"A pass does not add sections to the core: propose it with {ConsolidationPrompt.ProposeTool}."))
            : null;
    }

    private static string AddedTo(MemoryChange change, string path)
    {
        var unit = change.RowCount is null ? "line" : "row";
        return change.Created
            ? $"Created {path} with {Plural(Math.Max(change.Added, 0), unit)}"
            : $"Added {Plural(Math.Max(change.Added, 0), unit)} to {path}";
    }
}
