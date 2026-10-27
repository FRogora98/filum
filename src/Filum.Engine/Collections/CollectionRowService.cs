using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Memory.Files;
using Filum.Engine.Memory;

namespace Filum.Engine.Collections;

// Rows come as field → value; the code writes the CSV.
internal sealed class CollectionRowService(MemoryWriter writer)
{
    // A missing collection is created with the rows' fields; an unknown field is refused, listing the collection's
    // fields.
    public Task<MemoryOutcome<MemoryChange>> AddRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Append, (valid, file, _) =>
        {
            if (NotACollection(valid) is { } refusal)
            {
                return refusal;
            }

            if (rows.Count == 0 || rows.Any(r => r.Count == 0))
            {
                return ChangeTarget.Refuse("Give at least one row, each with at least one field.");
            }

            if (file is null)
            {
                var header = rows.SelectMany(r => r.Keys).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new ChangeTarget(valid, Csv.Write(header, rows.Select(r => Row(header, r))), MemorySensitivity.Normal, Deleted: false);
            }

            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return ChangeTarget.Refuse(FileRefusals.NotAValidCollection(path, table.Error));
            }

            if (UnknownFields(valid, table, rows.SelectMany(r => r.Keys)) is { } unknown)
            {
                return unknown;
            }

            var all = table.Rows.Select(r => r.Fields).Concat(rows.Select(r => Row(table.Header, r)));
            return new ChangeTarget(file.Path, Csv.Write(table.Header, all), file.Sensitivity, Deleted: false);
        }, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> UpdateRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IReadOnlyDictionary<string, string> set, CancellationToken cancellationToken) =>
        ChangeRowsAsync(userId, actor, path, where, set.Keys, (table, matches) =>
        {
            if (set.Count == 0)
            {
                return ChangeTarget.Refuse("Say which fields to change and their new values.");
            }

            var rows = table.Rows.Select((r, i) =>
            {
                if (!matches.Contains(i))
                {
                    return r.Fields;
                }

                var fields = r.Fields.ToArray();
                foreach (var (field, value) in set)
                {
                    fields[IndexOf(table, field)] = value;
                }

                return (IReadOnlyList<string>)fields;
            });
            return Csv.Write(table.Header, rows);
        }, cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> RemoveRowsAsync(Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, CancellationToken cancellationToken) =>
        ChangeRowsAsync(userId, actor, path, where, [], (table, matches) =>
            Csv.Write(table.Header, table.Rows.Where((_, i) => !matches.Contains(i)).Select(r => r.Fields)), cancellationToken);

    public Task<MemoryOutcome<MemoryChange>> AddFieldAsync(Guid userId, MemoryActor actor, string? path, string? field, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Edit, (valid, file, _) =>
        {
            if (NotACollection(valid) is { } refusal)
            {
                return refusal;
            }

            if (file is null)
            {
                return ChangeTarget.NotFound(FileRefusals.NoFileAt(path));
            }

            var name = field?.Trim() ?? string.Empty;
            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return ChangeTarget.Refuse(FileRefusals.NotAValidCollection(path, table.Error));
            }

            if (name.Length == 0 || table.Header.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return ChangeTarget.Refuse(name.Length == 0 ? "Give the name of the field to add." : $"{path} already has the field '{name}'.");
            }

            return new ChangeTarget(file.Path, Csv.Write([.. table.Header, name], table.Rows.Select(r => (IReadOnlyList<string>)[.. r.Fields, string.Empty])), file.Sensitivity, Deleted: false);
        }, cancellationToken);

    private Task<MemoryOutcome<MemoryChange>> ChangeRowsAsync(
        Guid userId, MemoryActor actor, string? path, IReadOnlyDictionary<string, string> where, IEnumerable<string> alsoFields,
        Func<CsvTable, HashSet<int>, object> change, CancellationToken cancellationToken) =>
        writer.ChangeAsync(userId, actor, path, MemoryOperation.Edit, (valid, file, _) =>
        {
            if (NotACollection(valid) is { } refusal)
            {
                return refusal;
            }

            if (file is null)
            {
                return ChangeTarget.NotFound(FileRefusals.NoFileAt(path));
            }

            if (where.Count == 0)
            {
                return ChangeTarget.Refuse("Say which rows: give one or more fields and the values they have.");
            }

            var table = Csv.Check(file.Content);
            if (!table.IsValid)
            {
                return ChangeTarget.Refuse(FileRefusals.NotAValidCollection(path, table.Error));
            }

            if (UnknownFields(valid, table, where.Keys.Concat(alsoFields)) is { } unknown)
            {
                return unknown;
            }

            var matches = Matches(table, where);
            if (matches.Count == 0)
            {
                return ChangeTarget.Refuse($"No row of {path} has {string.Join(" and ", where.Select(w => $"{w.Key} = '{w.Value}'"))}; read it to see its rows.");
            }

            return change(table, matches) switch
            {
                ChangeTarget target => target,
                string content => new ChangeTarget(file.Path, content, file.Sensitivity, Deleted: false),
                _ => ChangeTarget.Refuse(FileRefusals.NothingToChange)
            };
        }, cancellationToken);

    private static HashSet<int> Matches(CsvTable table, IReadOnlyDictionary<string, string> where) =>
        table.Rows
            .Select((r, i) => (r, i))
            .Where(x => where.All(w => string.Equals(x.r.Fields[IndexOf(table, w.Key)].Trim(), w.Value.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.i)
            .ToHashSet();

    private static ChangeTarget? NotACollection(string path) =>
        MemoryPaths.KindOf(path) == MemoryFileKind.Collection ? null : ChangeTarget.Refuse($"{path} is a document; rows belong in a collection, a path ending in .csv.");

    private static ChangeTarget? UnknownFields(string path, CsvTable table, IEnumerable<string> fields)
    {
        var unknown = fields.Select(f => f.Trim()).Where(f => !table.Header.Contains(f, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return unknown.Count == 0
            ? null
            : ChangeTarget.Refuse($"{path} has no field {string.Join(", ", unknown.Select(u => $"'{u}'"))}; its fields are {string.Join(", ", table.Header)}. Add a field on purpose with collection_add_field.");
    }

    private static int IndexOf(CsvTable table, string field) =>
        table.Header.ToList().FindIndex(h => string.Equals(h, field.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Row(IReadOnlyList<string> header, IReadOnlyDictionary<string, string> values) =>
        header.Select(h => values.FirstOrDefault(v => string.Equals(v.Key.Trim(), h, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty).ToList();
}
