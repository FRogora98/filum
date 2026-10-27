namespace Filum.Engine.Collections.Periods;

// In the order requests list them; a new period is a new ICalendarPeriodStrategy.
internal static class CalendarPeriods
{
    public static readonly IReadOnlyList<ICalendarPeriodStrategy> All = [new DayPeriodStrategy(), new WeekPeriodStrategy(), new MonthPeriodStrategy(), new YearPeriodStrategy()];

    public static ICalendarPeriodStrategy? Find(string name) => All.FirstOrDefault(p => p.Name == name);
}
