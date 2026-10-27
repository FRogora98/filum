using System.Globalization;

namespace Filum.Engine.Collections;

// Numbers in invariant culture (no thousands separator), dates as yyyy-MM-dd optionally followed by a time. Nothing is
// guessed.
internal static class CollectionValues
{
    private const string DateFormat = EngineConstants.Dates.Day;

    public static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    public static DateOnly? ParseDate(string text)
    {
        text = text.Trim();
        if (text.Length > DateFormat.Length && text[DateFormat.Length] is 'T' or ' ')
        {
            text = text[..DateFormat.Length];
        }

        return DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }
}
