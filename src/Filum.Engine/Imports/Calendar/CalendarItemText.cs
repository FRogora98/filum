using System.Globalization;

namespace Filum.Engine.Imports.Calendar;

internal static class CalendarItemText
{
    public static string Format(string summary, DateTimeOffset? start, bool allDay, string location, string description) =>
        ImportText.Cut($"Calendar: {(summary.Length == 0 ? "(no title)" : summary)}"
            + (start is null ? string.Empty : $" · {Start(start.Value, allDay)}")
            + (location.Length == 0 ? string.Empty : $" · {location}")
            + (description.Length == 0 ? string.Empty : $"\n{description}"));

    private static string Start(DateTimeOffset start, bool allDay) =>
        allDay
            ? start.ToString(EngineConstants.Dates.Day, CultureInfo.InvariantCulture) + " (all day)"
            : start.ToString(EngineConstants.Dates.DayAndMinute, CultureInfo.InvariantCulture);
}
