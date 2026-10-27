using System.Globalization;
using System.Text.RegularExpressions;

namespace Filum.Engine.Imports.Mail;

// RFC 2822, with the variants mail programs write.
internal static class MailDate
{
    private static readonly string[] Formats = ["d MMM yyyy HH:mm:ss zzz", "d MMM yyyy HH:mm zzz", "d MMM yy HH:mm:ss zzz", "d MMM yyyy HH:mm:ss"];

    public static DateTimeOffset? Parse(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var text = Regex.Replace(header, @"\([^)]*\)", string.Empty).Trim();
        text = Regex.Replace(text, @"^[A-Za-z]{3},\s*", string.Empty);
        var normalized = Regex.Replace(Regex.Replace(text, @"([+-]\d{2})(\d{2})$", "$1:$2"), @"\s(UTC|GMT|UT|Z)$", " +00:00");
        return DateTimeOffset.TryParseExact(normalized, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var exact) ? exact
            : DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var loose) ? loose
            : null;
    }
}
