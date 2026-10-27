namespace Filum.Engine.Collections.Trends;

// A robust z-score (median and median absolute deviation), so the outliers do not hide one another.
internal static class TrendAnomalyFinder
{
    // The first periods only start the smoothing: their errors say nothing about the series.
    private const int WarmUpPeriods = 2;

    // Makes the median absolute deviation comparable to a standard deviation for normally distributed errors.
    private const double MadToStandardDeviation = 0.6745;

    private const double Threshold = 3.5;
    private const int DeviationDecimals = 1;

    public static List<TrendAnomaly> Find(IReadOnlyList<TrendPoint> series, double[] values, double[] residuals)
    {
        var anomalies = new List<TrendAnomaly>();
        var errors = residuals.Skip(WarmUpPeriods).ToArray();
        var median = Median(errors);
        var mad = Median(errors.Select(e => Math.Abs(e - median)).ToArray());
        if (mad > 0)
        {
            for (var t = WarmUpPeriods; t < values.Length; t++)
            {
                var z = MadToStandardDeviation * (residuals[t] - median) / mad;
                if (Math.Abs(z) > Threshold)
                {
                    anomalies.Add(new TrendAnomaly(series[t].Period, values[t], values[t] - residuals[t], Math.Round(z, DeviationDecimals)));
                }
            }
        }

        return anomalies;
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
