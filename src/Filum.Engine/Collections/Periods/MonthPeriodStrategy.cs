using System.Globalization;

namespace Filum.Engine.Collections.Periods;

internal sealed class MonthPeriodStrategy : ICalendarPeriodStrategy
{
    private const string LabelFormat = "yyyy-MM";

    public string Name => "month";

    public string Label(DateTime date) => date.ToString(LabelFormat, CultureInfo.InvariantCulture);

    public DateTime Start(string label) => DateTime.ParseExact(label, LabelFormat, CultureInfo.InvariantCulture);

    public DateTime Advance(DateTime start, int count) => start.AddMonths(count);
}
