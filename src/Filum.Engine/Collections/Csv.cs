using System.Text;
using Filum.Engine.Abstractions.Collections;

namespace Filum.Engine.Collections;

/// <summary>
/// RFC 4180 CSV: comma-separated, fields optionally in double quotes (with <c>""</c> for a quote and line breaks
/// allowed inside quotes). Blank lines are ignored. A collection's first record is its header, and every row must
/// have exactly as many fields as the header.
/// </summary>
public static class Csv
{
    /// <summary>The content as a collection: its header and rows, or why it is not a valid one.</summary>
    public static CsvTable Check(string content)
    {
        var (records, parseError) = Parse(content);
        if (parseError is not null)
        {
            return new CsvTable([], [], parseError);
        }

        if (records.Count == 0)
        {
            return new CsvTable([], [], "A collection needs a header line with the names of its fields.");
        }

        var header = records[0].Fields.Select(f => f.Trim()).ToList();
        if (header.Any(string.IsNullOrEmpty))
        {
            return new CsvTable(header, [], "Every field of the header needs a name.");
        }

        var duplicate = header.GroupBy(h => h, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return new CsvTable(header, [], $"The header names the field '{duplicate.Key}' more than once.");
        }

        var rows = records.Skip(1).ToList();
        var wrong = rows.FirstOrDefault(r => r.Fields.Count != header.Count);
        if (wrong is not null)
        {
            return new CsvTable(header, rows, $"Line {wrong.Line} has {wrong.Fields.Count} fields, the header has {header.Count}.");
        }

        return new CsvTable(header, rows, null);
    }

    // Each field quoted only when it has to be.
    internal static string Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', header.Select(Field))).Append('\n');
        foreach (var row in rows)
        {
            text.Append(string.Join(',', row.Select(Field))).Append('\n');
        }

        return text.ToString();
    }

    internal static (IReadOnlyList<CsvRecord> Records, string? Error) Parse(string content) => new CsvReader(content).Read();

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 || value != value.Trim() ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
