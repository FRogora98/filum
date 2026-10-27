using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Imports.Mail;

namespace Filum.Engine.Imports.Parsers;

// Mails are separated by From lines after a blank line. A mail is identified by its Message-ID; without one, by its
// date, sender, subject and the start of its text.
internal sealed class MboxParserStrategy : IImportParserStrategy
{
    public const string Separator = "From ";
    private const string EscapedSeparator = ">From ";
    private const string Escape = ">";
    // The separator line a single raw mail is given, so it reads as a mailbox of one.
    private const string SingleMailSeparator = "From mail\n";
    private const string IdentityPrefix = "mbox|";
    private const int IdentityTextChars = 200;

    public string Format => ImportFormat.Mbox;

    public ImportParse Parse(string fileName, string content, DateTimeOffset now) => Parse(content);

    // Body lines that start with From are escaped first, as mbox requires.
    public static ImportItem? ParseMail(string rfc822)
    {
        var escaped = string.Join('\n', ImportText.Clean(rfc822).Split('\n').Select(l => l.StartsWith(Separator, StringComparison.Ordinal) ? Escape + l : l));
        return Parse(SingleMailSeparator + escaped).Items.SingleOrDefault();
    }

    private static ImportParse Parse(string content)
    {
        var items = new List<ImportItem>();
        var skipped = 0;
        foreach (var message in Messages(content))
        {
            if (Item(message) is { } item)
            {
                items.Add(item);
            }
            else
            {
                skipped++;
            }
        }

        return new ImportParse(items, skipped);
    }

    private static List<List<string>> Messages(string content)
    {
        var lines = ImportText.Clean(content).Split('\n');
        var messages = new List<List<string>>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(Separator, StringComparison.Ordinal) && (i == 0 || lines[i - 1].Length == 0))
            {
                messages.Add([]);
                continue;
            }

            if (messages.Count > 0)
            {
                messages[^1].Add(lines[i].StartsWith(EscapedSeparator, StringComparison.Ordinal) ? lines[i][1..] : lines[i]);
            }
        }

        return messages;
    }

    // Null when the mail has no sender, no subject and no text.
    private static ImportItem? Item(List<string> message)
    {
        var (headers, body) = MimeParts.Split(string.Join('\n', message));
        var text = MimeParts.ReadableText(headers, body).Trim();
        var from = MimeDecoder.Words(headers.GetValueOrDefault(MailHeaders.From) ?? string.Empty);
        var subject = MimeDecoder.Words(headers.GetValueOrDefault(MailHeaders.Subject) ?? string.Empty);
        if (from.Length == 0 && subject.Length == 0 && text.Length == 0)
        {
            return null;
        }

        var when = MailDate.Parse(headers.GetValueOrDefault(MailHeaders.Date));
        var id = headers.GetValueOrDefault(MailHeaders.MessageId)?.Trim();
        return new ImportItem(when, from.Length == 0 ? null : from,
            ImportText.Cut($"Mail from {(from.Length == 0 ? "unknown" : from)} · {(subject.Length == 0 ? "(no subject)" : subject)}\n{text}"),
            id is { Length: > 0 } ? $"{IdentityPrefix}{id}" : $"{IdentityPrefix}{when:O}|{from}|{subject}|{(text.Length > IdentityTextChars ? text[..IdentityTextChars] : text)}");
    }
}
