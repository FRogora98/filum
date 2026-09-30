using System.Text;

namespace Filum.Engine;

/// <summary>One record of a CSV file and the line it starts on (1-based).</summary>
public sealed record CsvRecord(int Line, IReadOnlyList<string> Fields);

/// <summary>A collection parsed and checked: its header, its rows, or why it is not a valid collection.</summary>
public sealed record CsvTable(IReadOnlyList<string> Header, IReadOnlyList<CsvRecord> Rows, string? Error)
{
    public bool IsValid => Error is null;
}

/// <summary>
/// RFC 4180 CSV: comma-separated, fields optionally in double quotes (with <c>""</c> for a quote and line breaks
/// allowed inside quotes). Blank lines are ignored. A collection's first record is its header, and every row must
/// have exactly as many fields as the header.
/// </summary>
public static class Csv
{
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

    /// <summary>A collection as text: the header, then one line per row, each field quoted only when it has to be.</summary>
    public static string Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', header.Select(Field))).Append('\n');
        foreach (var row in rows)
        {
            text.Append(string.Join(',', row.Select(Field))).Append('\n');
        }

        return text.ToString();
    }

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 || value != value.Trim() ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    public static (IReadOnlyList<CsvRecord> Records, string? Error) Parse(string content)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var inQuotes = false;
        var fieldWasQuoted = false;
        var recordHasContent = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord()
        {
            EndField();
            // A blank line is not a record.
            if (recordHasContent || fields.Count > 1)
            {
                records.Add(new CsvRecord(recordLine, fields.ToList()));
            }

            fields.Clear();
            recordHasContent = false;
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0 && !fieldWasQuoted:
                    inQuotes = true;
                    fieldWasQuoted = true;
                    recordHasContent = true;
                    break;
                case '"':
                    return (records, $"Line {line}: a quote inside an unquoted field; put the field in double quotes and write the quote as \"\".");
                case ',':
                    recordHasContent = true;
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                default:
                    if (fieldWasQuoted)
                    {
                        return (records, $"Line {line}: text after a closing quote.");
                    }

                    recordHasContent = true;
                    field.Append(c);
                    break;
            }
        }

        if (inQuotes)
        {
            return (records, $"Line {recordLine}: a quoted field is not closed.");
        }

        EndRecord();
        return (records, null);
    }
}
