using System.Globalization;

namespace Filum.Engine.Collections.Periods;

internal sealed class YearPeriodStrategy : ICalendarPeriodStrategy
{
    public string Name => "year";

    public string Label(DateTime date) => date.ToString("yyyy", CultureInfo.InvariantCulture);

    public DateTime Start(string label) => new(int.Parse(label, CultureInfo.InvariantCulture), 1, 1);

    public DateTime Advance(DateTime start, int count) => start.AddYears(count);
}
