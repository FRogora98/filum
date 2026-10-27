using System.Text.RegularExpressions;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Imports.Parsers;

namespace Filum.Engine.Imports;

internal static partial class ImportFormatDetector
{
    private const int LinesToSniff = 5;
    private const string IcsExtension = ".ics";
    private const string CalendarStart = "BEGIN:VCALENDAR";
    private const string MboxExtension = ".mbox";
    private const string MbxExtension = ".mbx";
    private const string CsvExtension = ".csv";

    public static string? Detect(string fileName, string content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var start = content.TrimStart(ImportText.ByteOrderMark, ' ', '\r', '\n');
        if (extension == IcsExtension || start.StartsWith(CalendarStart, StringComparison.OrdinalIgnoreCase))
        {
            return ImportFormat.Ics;
        }

        if (extension is MboxExtension or MbxExtension || (start.StartsWith(MboxParserStrategy.Separator, StringComparison.Ordinal) && MboxHeader().IsMatch(start)))
        {
            return ImportFormat.Mbox;
        }

        if (extension == CsvExtension)
        {
            return ImportFormat.Csv;
        }

        var firstLines = ImportText.Clean(start).Split('\n').Where(l => l.Trim().Length > 0).Take(LinesToSniff);
        return firstLines.Any(l => WhatsAppParserStrategy.MessageStart().IsMatch(l)) ? ImportFormat.WhatsApp : null;
    }

    [GeneratedRegex(@"^From \S+.*\n(?:[\w-]+:)", RegexOptions.Multiline)]
    private static partial Regex MboxHeader();
}
