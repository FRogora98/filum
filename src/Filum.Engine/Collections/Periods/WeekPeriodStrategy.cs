using System.Globalization;

namespace Filum.Engine.Collections.Periods;

// Labelled yyyy-Www with its ISO year, which can differ from the calendar year around New Year.
internal sealed class WeekPeriodStrategy : ICalendarPeriodStrategy
{
    private const int DaysInWeek = 7;
    private const int YearDigits = 4;
    private const int WeekNumberOffset = 6;

    public string Name => "week";

    public string Label(DateTime date) => $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):00}";

    public DateTime Start(string label) =>
        ISOWeek.ToDateTime(int.Parse(label[..YearDigits], CultureInfo.InvariantCulture), int.Parse(label[WeekNumberOffset..], CultureInfo.InvariantCulture), DayOfWeek.Monday);

    public DateTime Advance(DateTime start, int count) => start.AddDays(DaysInWeek * count);
}
