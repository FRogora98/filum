using System.Globalization;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Collections;

namespace Filum.Engine.Imports.Parsers;

// One item per row with a value, dated by the first column whose every value is a date; with none, by the time of the
// import.
internal sealed class CsvParserStrategy : IImportParserStrategy
{
    private const int NoDateColumn = -1;

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ssK", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "d/M/yyyy", "dd.MM.yyyy"];

    public string Format => ImportFormat.Csv;

    public ImportParse Parse(string fileName, string content, DateTimeOffset now)
    {
        var table = Csv.Check(ImportText.Clean(content).TrimStart(ImportText.ByteOrderMark));
        if (!table.IsValid)
        {
            return new ImportParse([], 0, $"{fileName} is not a valid CSV: {table.Error}");
        }

        var dateColumn = DateColumn(table);
        var items = new List<ImportItem>();
        var skipped = 0;
        foreach (var row in table.Rows)
        {
            if (row.Fields.All(f => f.Trim().Length == 0))
            {
                skipped++;
                continue;
            }

            var text = string.Join("; ", table.Header.Select((h, i) => (h, v: row.Fields[i].Trim())).Where(x => x.v.Length > 0).Select(x => $"{x.h}: {x.v}"));
            var when = dateColumn < 0 ? now : Date(row.Fields[dateColumn]) ?? now;
            items.Add(new ImportItem(when, null, ImportText.Cut(text), $"csv|{string.Join('|', table.Header)}|{string.Join('|', row.Fields)}"));
        }

        return new ImportParse(items, skipped);
    }

    private static int DateColumn(CsvTable table) =>
        Enumerable.Range(0, table.Header.Count).FirstOrDefault(c =>
            table.Rows.Any(r => r.Fields[c].Trim().Length > 0) && table.Rows.All(r => r.Fields[c].Trim().Length == 0 || Date(r.Fields[c]) is not null), NoDateColumn);

    private static DateTimeOffset? Date(string value) =>
        DateTimeOffset.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
}
