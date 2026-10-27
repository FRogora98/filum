namespace Filum.Engine.Collections.Trends;

// Estimates, always declared as such.
internal sealed record TrendResult(
    string Operation, string? Column, string Period, IReadOnlyList<TrendPoint> Series,
    IReadOnlyList<TrendForecast> Forecast, double? BacktestError, double? NaiveError, IReadOnlyList<TrendAnomaly> Anomalies, string? Note);
