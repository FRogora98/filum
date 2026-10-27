using System.Diagnostics;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Collections.Aggregation;
using Filum.Engine.Collections.Periods;

namespace Filum.Engine.Collections.Trends;

// Plain code, no model: Holt's smoothing for the forecast, a backtest on the last periods for its error, a robust
// z-score for the anomalies.
internal static class TrendCalculator
{
    public const int MinForecast = 6;
    public const int MinAnomalies = 8;
    public const int MaxHorizon = 12;

    private const string DefaultOperation = "sum";
    private const string DefaultPeriod = "month";
    private const string Average = "average";

    // A two-sided 95% range under normally distributed errors.
    private const double Z95 = 1.96;

    // The backtest holds out about this fraction of the series (one period at least, the horizon at most).
    private const int BacktestShareDivisor = 4;

    private static readonly string[] ForecastOperations = [DefaultOperation, "count", Average];

    public static MemoryOutcome<TrendResult> Compute(CsvTable table, string? dateColumn, string? column, string? operation, string? period, int horizon)
    {
        operation = (operation ?? DefaultOperation).Trim().ToLowerInvariant();
        period = (period ?? DefaultPeriod).Trim().ToLowerInvariant();
        if (!ForecastOperations.Contains(operation))
        {
            return MemoryOutcome<TrendResult>.Refused("The operation must be sum, count or average.");
        }

        if (horizon is < 1 or > MaxHorizon)
        {
            return MemoryOutcome<TrendResult>.Refused($"The horizon must be between 1 and {MaxHorizon} periods.");
        }

        var grouped = CollectionAggregator.Compute(table, new AggregateRequest(operation, column, dateColumn, period));
        if (grouped.IsRefused)
        {
            return MemoryOutcome<TrendResult>.Refused(grouped.Refusal);
        }

        // The aggregator has refused any period it does not know.
        var calendar = CalendarPeriods.Find(period) ?? throw new UnreachableException();
        var points = grouped.Value.Groups
            .Where(g => g.Key is not null)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key is { } key ? new TrendPoint(key, (double)g.Value) : null)
            .OfType<TrendPoint>()
            .ToList();
        var series = TrendSeries.Contiguous(points, calendar, fillZero: operation != Average);
        if (series.Count < MinForecast)
        {
            return MemoryOutcome<TrendResult>.Ok(new TrendResult(operation, column, period, series, [], null, null, [],
                $"Only {series.Count} {period}s with data: at least {MinForecast} are needed to forecast."));
        }

        var values = series.Select(p => p.Value).ToArray();
        var (alpha, beta) = HoltSmoothing.Fit(values);
        var (level, trend, residuals) = HoltSmoothing.Run(values, alpha, beta);
        var forecast = Forecast(series[^1].Period, calendar, horizon, level, trend, residuals);
        var (backtest, naive) = Backtest(values, horizon);
        var anomalies = values.Length >= MinAnomalies ? TrendAnomalyFinder.Find(series, values, residuals) : [];
        return MemoryOutcome<TrendResult>.Ok(new TrendResult(operation, column, period, series, forecast, backtest, naive, anomalies, null));
    }

    private static List<TrendForecast> Forecast(string last, ICalendarPeriodStrategy calendar, int horizon, double level, double trend, double[] residuals)
    {
        var rmse = Math.Sqrt(residuals.Skip(1).Select(r => r * r).DefaultIfEmpty(0).Average());
        return TrendSeries.Next(last, calendar, horizon).Select((p, i) =>
        {
            var estimate = level + (i + 1) * trend;
            var spread = Z95 * rmse * Math.Sqrt(i + 1);
            return new TrendForecast(p, estimate, estimate - spread, estimate + spread);
        }).ToList();
    }

    // The last periods forecast from the ones before them, against a naive forecast.
    private static (double Backtest, double Naive) Backtest(double[] values, int horizon)
    {
        var held = Math.Clamp(values.Length / BacktestShareDivisor, 1, horizon);
        var past = values[..^held];
        var (pastAlpha, pastBeta) = HoltSmoothing.Fit(past);
        var (pastLevel, pastTrend, _) = HoltSmoothing.Run(past, pastAlpha, pastBeta);
        var backtest = Enumerable.Range(0, held).Average(i => Math.Abs(values[past.Length + i] - (pastLevel + (i + 1) * pastTrend)));
        var naive = Enumerable.Range(0, held).Average(i => Math.Abs(values[past.Length + i] - past[^1]));
        return (backtest, naive);
    }
}
