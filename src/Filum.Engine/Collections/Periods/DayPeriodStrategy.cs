using System.Globalization;

namespace Filum.Engine.Collections.Periods;

internal sealed class DayPeriodStrategy : ICalendarPeriodStrategy
{
    private const string LabelFormat = EngineConstants.Dates.Day;

    public string Name => "day";

    public string Label(DateTime date) => date.ToString(LabelFormat, CultureInfo.InvariantCulture);

    public DateTime Start(string label) => DateTime.ParseExact(label, LabelFormat, CultureInfo.InvariantCulture);

    public DateTime Advance(DateTime start, int count) => start.AddDays(count);
}
