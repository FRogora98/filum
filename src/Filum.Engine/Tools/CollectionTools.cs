using System.ComponentModel;
using System.Text.Json;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;
using static Filum.Engine.Tools.ToolText;
using static Filum.Engine.Tools.TypedFileRefusals;

namespace Filum.Engine.Tools;

// The platform writes the file.
internal sealed class CollectionTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryWriter Writer => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(AddRows, ToolNames.CollectionAddRows,
            "Add rows to a collection (.csv); a missing one is created with the rows' fields."),
        AIFunctionFactory.Create(UpdateRows, ToolNames.CollectionUpdateRows,
            "Set fields of every row whose fields equal all of where."),
        AIFunctionFactory.Create(RemoveRows, ToolNames.CollectionRemoveRows,
            "Remove every row whose fields equal all of where."),
        AIFunctionFactory.Create(AddField, ToolNames.CollectionAddField,
            "Add a field to a collection; existing rows get it empty."),
    ];

    private Task<string> AddRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Rows, each field → value.")] List<Dictionary<string, JsonElement>> rows,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.CollectionAddRows, path, "add rows to", () => FactsOnly(path) ?? Writer.AddRowsAsync(UserId, Actor, path, rows.Select(Values).ToList(), cancellationToken),
            c => c.Created ? $"Created {path} with {Plural(c.RowCount ?? 0, "row")}" : $"Added {Plural(Math.Max(c.Added, 0), "row")} to {path}",
            c => $"{(c.Created ? "Created" : "Added to")} {path}: {Plural(c.RowCount ?? 0, "row")} now.", cancellationToken);

    private Task<string> UpdateRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Which rows: field → value, all matching.")] Dictionary<string, JsonElement> where,
        [Description("New values: field → value.")] Dictionary<string, JsonElement> set,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.CollectionUpdateRows, path, "change rows of", () => FactsOnly(path) ?? Writer.UpdateRowsAsync(UserId, Actor, path, Values(where), Values(set), cancellationToken),
            _ => $"Changed rows of {path}",
            c => $"Changed rows of {path}: {Plural(c.RowCount ?? 0, "row")}.", cancellationToken);

    private Task<string> RemoveRows(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Which rows: field → value, all matching.")] Dictionary<string, JsonElement> where,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.CollectionRemoveRows, path, "remove rows from", () => FactsOnly(path) ?? Writer.RemoveRowsAsync(UserId, Actor, path, Values(where), cancellationToken),
            c => $"Removed {Plural(Math.Max(-c.Added, 0), "row")} from {path}",
            c => $"Removed {Plural(Math.Max(-c.Added, 0), "row")} from {path}: {Plural(c.RowCount ?? 0, "row")} left.", cancellationToken);

    private Task<string> AddField(
        [Description("Path of the collection, ending in .csv.")] string path,
        [Description("Name of the new field.")] string field,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.CollectionAddField, path, "add a field to", () => FactsOnly(path) ?? Writer.AddFieldAsync(UserId, Actor, path, field, cancellationToken),
            _ => $"Added the field '{field}' to {path}",
            _ => $"Added the field '{field}' to {path}.", cancellationToken);

    // Strings as they are, numbers and booleans as written, null as empty.
    private static IReadOnlyDictionary<string, string> Values(Dictionary<string, JsonElement> values) =>
        values.ToDictionary(v => v.Key, v => v.Value.ValueKind switch
        {
            JsonValueKind.String => v.Value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => v.Value.GetRawText()
        });
}
