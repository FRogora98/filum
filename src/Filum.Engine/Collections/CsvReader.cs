using System.Text;
using Filum.Engine.Abstractions.Collections;

namespace Filum.Engine.Collections;

// One instance per text.
internal sealed class CsvReader(string content)
{
    private readonly List<CsvRecord> _records = [];
    private readonly List<string> _fields = [];
    private readonly StringBuilder _field = new();
    private int _line = 1;
    private int _recordLine = 1;
    private bool _inQuotes;
    private bool _fieldWasQuoted;
    private bool _recordHasContent;

    public (IReadOnlyList<CsvRecord> Records, string? Error) Read()
    {
        for (var i = 0; i < content.Length; i++)
        {
            if (_inQuotes)
            {
                i = ReadQuoted(i);
                continue;
            }

            if (ReadUnquoted(content[i]) is { } error)
            {
                return (_records, error);
            }
        }

        if (_inQuotes)
        {
            return (_records, $"Line {_recordLine}: a quoted field is not closed.");
        }

        EndRecord();
        return (_records, null);
    }

    // Returns the index of the last character it used (two for an escaped quote).
    private int ReadQuoted(int i)
    {
        var c = content[i];
        if (c == '"')
        {
            if (i + 1 < content.Length && content[i + 1] == '"')
            {
                _field.Append('"');
                return i + 1;
            }

            _inQuotes = false;
            return i;
        }

        if (c == '\n')
        {
            _line++;
        }

        _field.Append(c);
        return i;
    }

    private string? ReadUnquoted(char c)
    {
        switch (c)
        {
            case '"' when _field.Length == 0 && !_fieldWasQuoted:
                _inQuotes = true;
                _fieldWasQuoted = true;
                _recordHasContent = true;
                break;
            case '"':
                return $"Line {_line}: a quote inside an unquoted field; put the field in double quotes and write the quote as \"\".";
            case ',':
                _recordHasContent = true;
                EndField();
                break;
            case '\r':
                break;
            case '\n':
                EndRecord();
                _line++;
                _recordLine = _line;
                break;
            default:
                if (_fieldWasQuoted)
                {
                    return $"Line {_line}: text after a closing quote.";
                }

                _recordHasContent = true;
                _field.Append(c);
                break;
        }

        return null;
    }

    private void EndField()
    {
        _fields.Add(_field.ToString());
        _field.Clear();
        _fieldWasQuoted = false;
    }

    private void EndRecord()
    {
        EndField();
        // A blank line is not a record.
        if (_recordHasContent || _fields.Count > 1)
        {
            _records.Add(new CsvRecord(_recordLine, _fields.ToList()));
        }

        _fields.Clear();
        _recordHasContent = false;
    }
}
