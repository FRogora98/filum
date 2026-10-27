using System.Globalization;
using System.Text.RegularExpressions;

namespace Filum.Engine.Imports.Calendar;

internal static partial class IcsValues
{
    private const char Utc = 'Z';

    // yyyyMMdd: the value of an all-day start.
    public const int DateOnlyLength = 8;

    private static readonly string[] DateFormats = ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm", "yyyyMMdd"];

    // Z is UTC, a TZID is converted when the system knows it, anything else is taken as UTC.
    public static DateTimeOffset? Date(string value, string parameters)
    {
        var v = value.Trim();
        if (!DateTime.TryParseExact(v.TrimEnd(Utc), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        if (!v.EndsWith(Utc) && v.Length > DateOnlyLength && TimeZoneParameter().Match(parameters) is { Success: true } zone)
        {
            try
            {
                var offset = TimeZoneInfo.FindSystemTimeZoneById(zone.Groups[1].Value).GetUtcOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified));
                return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), offset).ToUniversalTime();
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
            {
                // A zone this system does not know: the time is taken as UTC, like a start with no zone.
            }
        }

        return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero);
    }

    public static string Unescape(string value) =>
        value.Replace("\\n", "\n").Replace("\\N", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\").Trim();

    [GeneratedRegex(@"TZID=""?([^;:""]+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex TimeZoneParameter();
}
