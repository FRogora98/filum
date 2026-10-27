using Filum.Engine.Collections.Periods;

namespace Filum.Engine.Collections.Trends;

internal static class TrendSeries
{
    // A guard against a runaway gap between labels.
    private const int MaxPeriods = 10_000;

    // A sum or a count of a period with no rows is zero.
    public static List<TrendPoint> Contiguous(List<TrendPoint> points, ICalendarPeriodStrategy period, bool fillZero)
    {
        if (points.Count == 0 || !fillZero)
        {
            return points;
        }

        var byPeriod = points.ToDictionary(p => p.Period);
        var all = new List<TrendPoint> { points[0] };
        var current = points[0].Period;
        while (string.CompareOrdinal(current, points[^1].Period) < 0 && all.Count < MaxPeriods)
        {
            current = Next(current, period, 1)[0];
            all.Add(byPeriod.TryGetValue(current, out var p) ? p : new TrendPoint(current, 0));
        }

        return all;
    }

    public static IReadOnlyList<string> Next(string label, ICalendarPeriodStrategy period, int count)
    {
        var start = period.Start(label);
        return Enumerable.Range(1, count).Select(i => period.Label(period.Advance(start, i))).ToList();
    }
}
