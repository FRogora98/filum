using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;

namespace Filum.Engine.Imports.Parsers;

// Whether the day or the month comes first is read from the whole file.
internal sealed partial class WhatsAppParserStrategy : IImportParserStrategy
{
    private const int MonthsInYear = 12;
    private const int HoursInHalfDay = 12;
    private const int TwoDigitYearLimit = 100;
    private const int TwoDigitYearCentury = 2000;
    private const string Pm = "PM";
    private const string Am = "AM";
    private const string SenderSeparator = ": ";
    private const string IdentityPrefix = "whatsapp|";

    // The groups of MessageStart: the two numbers of the date (day and month in either order), the year, the time.
    private const string First = "a";
    private const string Second = "b";
    private const string Year = "y";
    private const string Hour = "h";
    private const string Minute = "m";
    private const string SecondOfMinute = "s";
    private const string Meridiem = "ampm";
    private const string Rest = "rest";

    public string Format => ImportFormat.WhatsApp;

    public ImportParse Parse(string fileName, string content, DateTimeOffset now)
    {
        var chat = ChatName(fileName);
        var lines = ImportText.Clean(content).Split('\n');
        var starts = lines.Select(l => MessageStart().Match(l)).ToList();
        var dayFirst = IsDayFirst(starts);

        var items = new List<ImportItem>();
        var skipped = 0;
        (DateTimeOffset When, string Who, StringBuilder Text)? current = null;
        void Flush()
        {
            if (current is { } c)
            {
                var text = c.Text.ToString().Trim();
                items.Add(new ImportItem(c.When, c.Who, ImportText.Cut($"{c.Who}: {text}"), $"{IdentityPrefix}{chat}|{c.When:O}|{c.Who}|{text}"));
            }

            current = null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var m = starts[i];
            if (!m.Success)
            {
                if (current is { } open && lines[i].Length > 0)
                {
                    open.Text.Append('\n').Append(lines[i]);
                }

                continue;
            }

            Flush();
            current = Message(m, dayFirst);
            skipped += current is null ? 1 : 0;
        }

        Flush();
        return new ImportParse(items, skipped);
    }

    private static string ChatName(string fileName)
    {
        var chat = Path.GetFileNameWithoutExtension(fileName);
        var named = ChatNamePattern().Match(chat);
        return named.Success ? named.Groups[1].Value.Trim() : chat;
    }

    // Day first unless a line has a first number that can only be a month and a second that can only be a day.
    private static bool IsDayFirst(List<Match> starts) =>
        !starts.Any(m => m.Success && Number(m, First) <= MonthsInYear && Number(m, Second) > MonthsInYear)
        || starts.Any(m => m.Success && Number(m, First) > MonthsInYear);

    // Null for a notice with no sender or a line with an impossible date.
    private static (DateTimeOffset When, string Who, StringBuilder Text)? Message(Match m, bool dayFirst)
    {
        var rest = m.Groups[Rest].Value;
        var colon = rest.IndexOf(SenderSeparator, StringComparison.Ordinal);
        var when = Time(m, dayFirst);
        if (colon <= 0 || when is null)
        {
            return null;
        }

        return (when.Value, rest[..colon].Trim(), new StringBuilder(rest[(colon + SenderSeparator.Length)..]));
    }

    private static DateTimeOffset? Time(Match m, bool dayFirst)
    {
        var (a, b, year) = (Number(m, First), Number(m, Second), Number(m, Year));
        var (day, month) = dayFirst ? (a, b) : (b, a);
        year = year < TwoDigitYearLimit ? TwoDigitYearCentury + year : year;
        var hour = Number(m, Hour);
        var ampm = m.Groups[Meridiem].Value.ToUpperInvariant();
        if (ampm == Pm && hour < HoursInHalfDay)
        {
            hour += HoursInHalfDay;
        }
        else if (ampm == Am && hour == HoursInHalfDay)
        {
            hour = 0;
        }

        var second = m.Groups[SecondOfMinute].Success ? Number(m, SecondOfMinute) : 0;
        try
        {
            return new DateTimeOffset(year, month, day, hour, Number(m, Minute), second, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int Number(Match m, string group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\[?(?<a>\d{1,2})[/.\-](?<b>\d{1,2})[/.\-](?<y>\d{2,4}),?\s+(?<h>\d{1,2})[:.](?<m>\d{2})(?:[:.](?<s>\d{2}))?\s?(?<ampm>[AaPp]\.?[Mm]\.?)?\]?\s(?:-\s)?(?<rest>.*)$")]
    internal static partial Regex MessageStart();

    [GeneratedRegex(@"^WhatsApp Chat (?:with|con|mit|avec) (.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChatNamePattern();
}
