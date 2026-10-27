using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Imports;

namespace Filum.Engine.UnitTests.Imports;

public sealed class ImporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_WhenAnAndroidWhatsAppExportHasTheDayFirst_ShouldGiveOneItemPerMessageAndSkipNotices()
    {
        var file = string.Join("\r\n",
            "13/03/24, 09:15 - Messages and calls are end-to-end encrypted. No one outside of this chat can read them.",
            "13/03/24, 09:16 - Sam: The keys are with the neighbour",
            "and the bike is in the shed.",
            "14/03/24, 18:02 - Alex Doe: Thanks!",
            "‎14/03/24, 18:03 - Sam: <Media omitted>");

        var parsed = Importer.Parse(ImportFormat.WhatsApp, "WhatsApp Chat with Sam.txt", file, Now);

        Assert.Equal(1, parsed.Skipped);
        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal("Sam: The keys are with the neighbour\nand the bike is in the shed.", parsed.Items[0].Text);
        Assert.Equal(new DateTimeOffset(2024, 3, 13, 9, 16, 0, TimeSpan.Zero), parsed.Items[0].When);
        Assert.Equal(("Alex Doe", new DateTimeOffset(2024, 3, 14, 18, 2, 0, TimeSpan.Zero)), (parsed.Items[1].Who, parsed.Items[1].When!.Value));
        Assert.StartsWith("whatsapp|Sam|", parsed.Items[0].Identity);
        Assert.Equal(ImportFormat.WhatsApp, Importer.Detect("WhatsApp Chat with Sam.txt", file));
    }

    [Fact]
    public void Parse_WhenAnIosWhatsAppExportHasTheMonthFirstAndAmPm_ShouldReadItRight()
    {
        var file = "[3/14/24, 9:05:12 PM] Sam: Dinner at eight?\n[3/15/24, 12:01:00 AM] Robin: Yes";

        var parsed = Importer.Parse(ImportFormat.WhatsApp, "_chat.txt", file, Now);

        Assert.Equal(new DateTimeOffset(2024, 3, 14, 21, 5, 12, TimeSpan.Zero), parsed.Items[0].When);
        Assert.Equal(new DateTimeOffset(2024, 3, 15, 0, 1, 0, TimeSpan.Zero), parsed.Items[1].When);
        Assert.Equal("Robin: Yes", parsed.Items[1].Text);
    }

    [Fact]
    public void Parse_WhenTheFileIsAnMbox_ShouldGiveOneItemPerMailWithItsDateSenderSubjectAndReadableBody()
    {
        var file = string.Join("\n",
            "From sam@example.invalid Mon Mar 11 10:00:00 2024",
            "From: Sam <sam@example.invalid>",
            "Subject: Plans",
            "Date: Mon, 11 Mar 2024 10:00:00 +0100",
            "Message-ID: <one@example.invalid>",
            "",
            "Meet at the station.",
            ">From the platform we walk.",
            "",
            "From robin@example.invalid Tue Mar 12 08:00:00 2024",
            "From: Robin <robin@example.invalid>",
            "Subject: =?UTF-8?B?Q2Fmw6k=?=",
            "Date: Tue, 12 Mar 2024 08:00:00 +0000 (UTC)",
            "Content-Type: multipart/alternative; boundary=\"b1\"",
            "",
            "--b1",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: quoted-printable",
            "",
            "The caf=C3=A9 opens at nine.",
            "--b1",
            "Content-Type: text/html",
            "",
            "<p>ignored</p>",
            "--b1--",
            "",
            "From kim@example.invalid Wed Mar 13 08:00:00 2024",
            "From: Kim <kim@example.invalid>",
            "Subject: Note",
            "Date: Wed, 13 Mar 2024 08:00:00 +0000",
            "Content-Type: text/html",
            "Content-Transfer-Encoding: base64",
            "",
            Convert.ToBase64String("<p>Bring the <b>blue</b> folder &amp; the map.</p>"u8.ToArray()),
            "");

        var parsed = Importer.Parse(ImportFormat.Mbox, "mail.mbox", file, Now);

        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal("Mail from Sam <sam@example.invalid> · Plans\nMeet at the station.\nFrom the platform we walk.", parsed.Items[0].Text);
        Assert.Equal(new DateTimeOffset(2024, 3, 11, 9, 0, 0, TimeSpan.Zero), parsed.Items[0].When!.Value.ToUniversalTime());
        Assert.Equal("mbox|<one@example.invalid>", parsed.Items[0].Identity);
        Assert.Equal("Mail from Robin <robin@example.invalid> · Café\nThe café opens at nine.", parsed.Items[1].Text);
        Assert.Equal("Mail from Kim <kim@example.invalid> · Note\nBring the blue folder & the map.", parsed.Items[2].Text);
        Assert.Equal(ImportFormat.Mbox, Importer.Detect("export", file));
    }

    [Fact]
    public void ParseMail_WhenTheMailIsRaw_ShouldParseLikeTheSameMailInAnMboxEvenWithALineStartingWithFrom()
    {
        var raw = string.Join("\r\n",
            "From: Sam <sam@example.invalid>",
            "Subject: Plans",
            "Date: Mon, 11 Mar 2024 10:00:00 +0100",
            "Message-ID: <one@example.invalid>",
            "",
            "Meet at the station.",
            "From the platform we walk.");
        var mbox = "From sam@example.invalid Mon Mar 11 10:00:00 2024\n" + raw.Replace("\r\n", "\n").Replace("\nFrom the", "\n>From the");

        var mail = Importer.ParseMail(raw);

        Assert.NotNull(mail);
        Assert.Equal(Importer.Parse(ImportFormat.Mbox, "mail.mbox", mbox, Now).Items.Single(), mail);
        Assert.Equal("mbox|<one@example.invalid>", mail.Identity);
        Assert.EndsWith("Meet at the station.\nFrom the platform we walk.", mail.Text);
        Assert.Null(Importer.ParseMail(""));
    }

    [Fact]
    public void Parse_WhenTheFileIsAnIcsCalendar_ShouldGiveOneItemPerEventWithItsStartTitlePlaceAndDescription()
    {
        var file = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "BEGIN:VEVENT",
            "UID:a1",
            "DTSTART:20240320T143000Z",
            "SUMMARY:Meeting\\, planning",
            "LOCATION:Main street 4",
            "DESCRIPTION:Bring the card\\nand the old x-",
            " rays",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:a2",
            "DTSTART;VALUE=DATE:20240401",
            "SUMMARY:Holiday",
            "END:VEVENT",
            "END:VCALENDAR");

        var parsed = Importer.Parse(ImportFormat.Ics, "cal.ics", file, Now);

        Assert.Equal(2, parsed.Items.Count);
        Assert.Equal("Calendar: Meeting, planning · 2024-03-20 14:30 · Main street 4\nBring the card\nand the old x-rays", parsed.Items[0].Text);
        Assert.Equal(new DateTimeOffset(2024, 3, 20, 14, 30, 0, TimeSpan.Zero), parsed.Items[0].When);
        Assert.Equal("Calendar: Holiday · 2024-04-01 (all day)", parsed.Items[1].Text);
        Assert.Equal(ImportFormat.Ics, Importer.Detect("x.txt", file));
    }

    [Fact]
    public void Parse_WhenACsvHasADateColumn_ShouldDateEachRowByItAndWithoutOneByTheImport()
    {
        var dated = Importer.Parse(ImportFormat.Csv, "log.csv", "note,day,count\nmorning,2024-03-02,5\n,,\nevening,2024-03-03,7\n", Now);
        var undated = Importer.Parse(ImportFormat.Csv, "list.csv", "item,qty\nrope,2\n", Now);

        Assert.Equal(["note: morning; day: 2024-03-02; count: 5", "note: evening; day: 2024-03-03; count: 7"], dated.Items.Select(i => i.Text));
        Assert.Equal(new DateTimeOffset(2024, 3, 3, 0, 0, 0, TimeSpan.Zero), dated.Items[1].When);
        Assert.Equal(1, dated.Skipped);
        Assert.Equal(Now, undated.Items[0].When);
        Assert.Contains("not a valid CSV", Importer.Parse(ImportFormat.Csv, "bad.csv", "a,b\n1,2,3\n", Now).Error);
    }

    [Fact]
    public void Parse_WhenACsvDateHasAZone_ShouldReadItsMomentWhereverTheImportRuns()
    {
        var parsed = Importer.Parse(ImportFormat.Csv, "log.csv", "when,note\n2025-01-15T10:00:00Z,a\n2025-01-15T10:00:00+02:00,b\n", Now);

        Assert.Equal([new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 1, 15, 8, 0, 0, TimeSpan.Zero)], parsed.Items.Select(i => i.When!.Value.ToUniversalTime()));
    }

    [Fact]
    public void Parse_WhenAFileHasTwoIdenticalItems_ShouldKeepTwoWithTheSameIdentitiesAcrossImports()
    {
        const string twice = "day,item\n2025-01-15,coffee\n2025-01-15,coffee\n";

        var first = Importer.Parse(ImportFormat.Csv, "log.csv", twice, Now);
        var again = Importer.Parse(ImportFormat.Csv, "log.csv", twice, Now);

        Assert.Equal(2, first.Items.Select(i => i.Identity).Distinct().Count());
        Assert.Equal(first.Items.Select(i => i.Identity), again.Items.Select(i => i.Identity));
    }

    [Fact]
    public void Parse_WhenAnEventHasAnAlarmAndAZonedStart_ShouldNotTakeTheAlarmAsItsDescriptionAndConvertTheStart()
    {
        var file = string.Join("\r\n",
            "BEGIN:VCALENDAR", "BEGIN:VEVENT", "UID:z1", "DTSTART;TZID=Europe/Rome:20250115T100000", "SUMMARY:Call",
            "BEGIN:VALARM", "ACTION:DISPLAY", "DESCRIPTION:Reminder", "END:VALARM",
            "DESCRIPTION:Real", "END:VEVENT", "END:VCALENDAR");

        var item = Assert.Single(Importer.Parse(ImportFormat.Ics, "cal.ics", file, Now).Items);

        Assert.EndsWith("\nReal", item.Text);
        Assert.DoesNotContain("Reminder", item.Text);
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero), item.When);
    }

    [Fact]
    public void Parse_WhenAMailDateHasANamedZone_ShouldReadIt()
    {
        var mail = "From a@example.invalid Wed Mar 13 08:00:00 2024\nFrom: A <a@example.invalid>\nSubject: S\nDate: Wed, 13 Mar 2024 08:00:00 UTC\n\nBody\n";

        Assert.Equal(new DateTimeOffset(2024, 3, 13, 8, 0, 0, TimeSpan.Zero), Importer.Parse(ImportFormat.Mbox, "m.mbox", mail, Now).Items.Single().When);
    }

    [Fact]
    public void IdOfAndDetect_WhenTheIdentityIsTheSameOrTheFileUnknown_ShouldGiveTheSameIdAndNoFormat()
    {
        Assert.Equal(Importer.IdOf("mbox|<one@example.invalid>"), Importer.IdOf("mbox|<one@example.invalid>"));
        Assert.NotEqual(Importer.IdOf("a"), Importer.IdOf("b"));
        Assert.Null(Importer.Detect("notes.txt", "just some text\nmore text"));
        Assert.Contains("one of", Importer.Parse("pdf", "x.pdf", "", Now).Error);
    }
}
