using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Imports;
using Filum.Engine.Imports;
using Filum.Engine.IntegrationTests.Snapshots;
using static Filum.Engine.IntegrationTests.Snapshots.SnapshotText;

namespace Filum.Engine.IntegrationTests.Imports;

// Pinned in a snapshot: an import identity that changes by one byte duplicates a person's events on the next import.
public sealed class ImporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Imports_WhenParsingEveryFormat_ShouldMatchTheirSnapshot()
    {
        var text = new StringBuilder();
        foreach (var (format, fileName, content) in ImportSamples())
        {
            var parsed = Importer.Parse(format, fileName, content, Now);
            text.Append("## ").Append(format).Append(' ').Append(fileName).Append('\n');
            text.Append("detect=").Append(Importer.Detect(fileName, content) ?? "null").Append(" skipped=").Append(parsed.Skipped.ToString(CultureInfo.InvariantCulture))
                .Append(" error=").Append(Escape(parsed.Error ?? "null")).Append('\n');
            foreach (var item in parsed.Items)
            {
                AppendItem(text, item);
            }
        }

        text.Append("## raw mails\n");
        foreach (var raw in RawMails())
        {
            if (Importer.ParseMail(raw) is { } item)
            {
                AppendItem(text, item);
            }
            else
            {
                text.Append("null\n");
            }
        }

        text.Append("## calendar text\n");
        text.Append(Escape(Importer.CalendarText("", null, false, "", ""))).Append('\n');
        text.Append(Escape(Importer.CalendarText("Trip", new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero), true, "Port", "Ferry"))).Append('\n');
        text.Append(Escape(Importer.CalendarText("Call", new DateTimeOffset(2025, 5, 1, 7, 45, 0, TimeSpan.Zero), false, "", new string('d', 4_100)))).Append('\n');

        SnapshotFile.Match("Imports.snapshot.txt", text.ToString());
    }

    private static void AppendItem(StringBuilder text, ImportItem item) =>
        text.Append(item.When?.ToString("O", CultureInfo.InvariantCulture) ?? "null").Append(" | ").Append(Escape(item.Who ?? "null")).Append(" | ")
            .Append(Escape(item.Text)).Append(" | ").Append(Escape(item.Identity)).Append(" | ").Append(Importer.IdOf(item.Identity).ToString("D")).Append('\n');

    private static IEnumerable<(string Format, string FileName, string Content)> ImportSamples() =>
        [
            .. WhatsAppSamples(),
            (ImportFormat.Mbox, "mail.mbox", string.Join("\n", [.. SamMail(), .. RobinMail(), .. KimMail(), .. LeeMail(), .. MalformedMails()])),
            (ImportFormat.Ics, "cal.ics", string.Join("\r\n", CalendarLines())),
            .. CsvSamples(),
            ("pdf", "x.pdf", ""),
            (ImportFormat.Ics, "notes.txt", "just some text\nmore text"),
        ];

    private static IEnumerable<(string Format, string FileName, string Content)> WhatsAppSamples()
    {
        yield return (ImportFormat.WhatsApp, "WhatsApp Chat with Sam.txt", string.Join("\r\n",
            "13/03/24, 09:15 - Messages and calls are end-to-end encrypted. No one outside of this chat can read them.",
            "13/03/24, 09:16 - Sam: The keys are with the neighbour",
            "and the bike is in the shed.",
            "",
            "14/03/24, 18:02 - Alex Doe: Thanks!",
            "\u200E14/03/24, 18:03 - Sam: <Media omitted>",
            "14/03/24, 18:03 - Sam: hi",
            "14/03/24, 18:03 - Sam: hi",
            "31/02/24, 10:00 - Sam: no such day",
            "15/03/2024, 07:00:30 - Robin:\u202Fwith\u00A0spaces"));
        yield return (ImportFormat.WhatsApp, "WhatsApp Chat con Gruppo.txt", "[3/14/24, 9:05:12 PM] Sam: Dinner at eight?\n[3/15/24, 12:01:00 AM] Robin: Yes\n[3/15/24, 12:30:00 PM] Robin: Lunch\n[03.16.24, 1.05 p.m.] Kim: dots");
        yield return (ImportFormat.WhatsApp, "_chat.txt", "1/2/24, 10:00 - A: ambiguous day and month\n2/1/24, 11:00 - B: " + new string('x', 4_100));
    }

    private static string[] SamMail() =>
    [
        "From sam@example.invalid Mon Mar 11 10:00:00 2024",
        "From: Sam <sam@example.invalid>",
        "Subject: Plans",
        "Date: Mon, 11 Mar 2024 10:00:00 +0100",
        "Message-ID: <one@example.invalid>",
        "",
        "Meet at the station.",
        ">From the platform we walk.",
        ""
    ];

    private static string[] RobinMail() =>
    [
        "From robin@example.invalid Tue Mar 12 08:00:00 2024",
        "From: Robin <robin@example.invalid>",
        "Subject: =?UTF-8?B?Q2Fmw6k=?= and =?iso-8859-1?Q?d=E9j=E0_vu?=",
        "Date: Tue, 12 Mar 2024 08:00:00 +0000 (UTC)",
        "Content-Type: multipart/alternative; boundary=\"b1\"",
        "",
        "--b1",
        "Content-Type: text/plain; charset=utf-8",
        "Content-Transfer-Encoding: quoted-printable",
        "",
        "The caf=C3=A9 opens at nine, a long line that is =",
        "soft broken.",
        "--b1",
        "Content-Type: text/html",
        "",
        "<p>ignored</p>",
        "--b1--",
        ""
    ];

    private static string[] KimMail() =>
    [
        "From kim@example.invalid Wed Mar 13 08:00:00 2024",
        "From: Kim <kim@example.invalid>",
        "Subject: Note",
        "Date: Wed, 13 Mar 2024 08:00:00 GMT",
        "Content-Type: text/html; charset=\"windows-1252\"",
        "Content-Transfer-Encoding: base64",
        "",
        Convert.ToBase64String("<style>p{}</style><p>Bring the <b>blue</b> folder &amp; the map.</p><br><div>Two</div>\n\n\n<li>x</li>"u8.ToArray()),
        ""
    ];

    private static string[] LeeMail() =>
    [
        "From lee@example.invalid Thu Mar 14 08:00:00 2024",
        "From: Lee <lee@example.invalid>",
        "Subject: Folded",
        "  subject line",
        "Subject: second subject ignored",
        "Date: 14 Mar 24 09:30:00 +0200",
        "Content-Type: multipart/mixed; boundary=outer",
        "",
        "--outer",
        "Content-Type: multipart/alternative; boundary=inner",
        "",
        "--inner",
        "Content-Type: text/html",
        "",
        "<p>Nested &lt;html&gt; only</p>",
        "--inner--",
        "--outer",
        "Content-Type: application/pdf",
        "",
        "binary",
        "--outer--",
        ""
    ];

    private static string[] MalformedMails() =>
    [
        "From x Fri Mar 15 08:00:00 2024",
        "Date: not a date at all",
        "Content-Type: text/plain; charset=nonexistent-charset",
        "Content-Transfer-Encoding: base64",
        "",
        "!!! not base64 !!!",
        "",
        "From y Fri Mar 15 08:00:00 2024",
        "X-Empty: yes",
        "",
        "",
        "From z Sat Mar 16 08:00:00 2024",
        "From: Zed <z@example.invalid>",
        "Date: Sat, 16 Mar 2024 08:00 -0500",
        "",
        new string('w', 300),
        "",
        "From z Sat Mar 16 08:00:00 2024",
        "From: Zed <z@example.invalid>",
        "Date: Sat, 16 Mar 2024 08:00 -0500",
        "",
        new string('w', 300),
        ""
    ];

    private static string[] CalendarLines() =>
    [
        "BEGIN:VCALENDAR",
        "BEGIN:VEVENT",
        "UID:a1",
        "DTSTART:20240320T143000Z",
        "SUMMARY:Meeting\\, planning\\; agenda \\\\ notes",
        "LOCATION:Main street 4",
        "DESCRIPTION:Bring the card\\nand the old x-",
        " rays\\Nand more",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "UID:a2",
        "DTSTART;VALUE=DATE:20240401",
        "SUMMARY:Holiday",
        "SUMMARY:Second summary ignored",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "DTSTART;TZID=Europe/Rome:20250115T100000",
        "SUMMARY:No uid",
        "BEGIN:VALARM",
        "ACTION:DISPLAY",
        "DESCRIPTION:Reminder",
        "END:VALARM",
        "DESCRIPTION:Real",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "DTSTART;TZID=\"Nowhere/Unknown\":20250115T1000",
        "SUMMARY:Unknown zone",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "LOCATION:Nowhere",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "DTSTART:garbage",
        "SUMMARY:Bad start",
        "badline",
        "END:VEVENT",
        "BEGIN:VEVENT",
        "UID:a1",
        "DTSTART:20240320T143000Z",
        "SUMMARY:Meeting\\, planning\\; agenda \\\\ notes",
        "END:VEVENT",
        "END:VCALENDAR"
    ];

    private static IEnumerable<(string Format, string FileName, string Content)> CsvSamples()
    {
        yield return (ImportFormat.Csv, "log.csv", "\uFEFFnote,day,count\r\nmorning,2024-03-02,5\n,,\nevening,03/03/2024,7\nnight,2024-03-04 21:30,1\nnight,2024-03-04 21:30,1\n");
        yield return (ImportFormat.Csv, "mixed.csv", "when,note\n2025-01-15T10:00:00Z,a\n2025-01-15T10:00:00+02:00,b\nlater,c\n");
        yield return (ImportFormat.Csv, "list.csv", "item,qty\nrope,2\n\"knot, tight\",\n");
        yield return (ImportFormat.Csv, "bad.csv", "a,b\n1,2,3\n");
    }

    private static IEnumerable<string> RawMails()
    {
        yield return string.Join("\r\n",
            "From: Sam <sam@example.invalid>",
            "Subject: Plans",
            "Date: Mon, 11 Mar 2024 10:00:00 +0100",
            "Message-ID: <one@example.invalid>",
            "",
            "Meet at the station.",
            "From the platform we walk.");
        yield return "Subject: no sender\n\nbody";
        yield return string.Empty;
    }
}
