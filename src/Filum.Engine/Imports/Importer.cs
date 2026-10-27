using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Imports.Calendar;
using Filum.Engine.Imports.Parsers;

namespace Filum.Engine.Imports;

/// <summary>
/// Turns files a person already has into items: chat exports, mailboxes, calendars, spreadsheets. No dependency and
/// no model: line rules, RFC 2822 dates, MIME decoded by hand, ICS unfolded.
/// </summary>
public static class Importer
{
    /// <summary>The longest text one item keeps.</summary>
    public const int MaxItemChars = 4_000;

    internal static string? Detect(string fileName, string content) => ImportFormatDetector.Detect(fileName, content);

    /// <summary>The items of a file in <paramref name="format"/>, dated <paramref name="now"/> when they carry no date, with the lines skipped; or why it cannot be read.</summary>
    public static ImportParse Parse(string format, string fileName, string content, DateTimeOffset now) =>
        ImportParsers.Find(format) is { } parser
            ? ImportIdentities.RankRepeats(parser.Parse(fileName, content, now))
            : new ImportParse([], 0, $"The format must be one of {string.Join(", ", ImportFormat.All)}.");

    // The same identity always gets the same id.
    internal static Guid IdOf(string identity) => ImportIdentities.IdOf(identity);

    /// <summary>
    /// One raw mail (RFC 822, as a mail service hands it) through the mbox rules, so its identity is the same as in a
    /// mailbox file; null when it carries nothing. Body lines that start with "From " are escaped first.
    /// </summary>
    public static ImportItem? ParseMail(string rfc822) => MboxParserStrategy.ParseMail(rfc822);

    /// <summary>
    /// How a calendar event reads as an item, whatever it came from (a file, a link, a calendar service): title, start
    /// (a day for an all-day event), place, then the description.
    /// </summary>
    public static string CalendarText(string summary, DateTimeOffset? start, bool allDay, string location, string description) =>
        CalendarItemText.Format(summary, start, allDay, location, description);
}
