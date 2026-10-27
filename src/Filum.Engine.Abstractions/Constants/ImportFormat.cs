namespace Filum.Engine.Abstractions.Constants;

/// <summary>The file formats an import reads.</summary>
public static class ImportFormat
{
    /// <summary>A WhatsApp chat export, one message per line.</summary>
    public const string WhatsApp = "whatsapp";

    /// <summary>A mailbox in the mbox format.</summary>
    public const string Mbox = "mbox";

    /// <summary>An iCalendar file of events.</summary>
    public const string Ics = "ics";

    /// <summary>A spreadsheet saved as comma-separated values.</summary>
    public const string Csv = "csv";

    /// <summary>Every format, in the order refusals list them.</summary>
    public static readonly IReadOnlyList<string> All = [WhatsApp, Mbox, Ics, Csv];
}
