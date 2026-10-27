using System.Globalization;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Collections;
using Filum.Engine.Collections.Trends;

namespace Filum.Engine.UnitTests.Collections.Trends;

public sealed class TrendCalculatorTests
{
    private static CsvTable Monthly(params double[] values) =>
        Csv.Check("date,amount\n" + string.Concat(values.Select((v, i) =>
            $"{new DateOnly(2025, 1, 15).AddMonths(i):yyyy-MM-dd},{v.ToString(CultureInfo.InvariantCulture)}\n")));

    [Fact]
    public void Compute_WhenTheSeriesRisesSteadily_ShouldForecastAlongItsLineWithASmallErrorOnItsOwnPast()
    {
        var result = TrendCalculator.Compute(Monthly([.. Enumerable.Range(0, 12).Select(i => 100.0 + 10 * i)]), "date", "amount", "sum", "month", 3).Value!;

        Assert.Equal(["2026-01", "2026-02", "2026-03"], result.Forecast.Select(f => f.Period));
        Assert.Equal([220.0, 230.0, 240.0], result.Forecast.Select(f => Math.Round(f.Estimate)));
        Assert.True(result.BacktestError < 1, $"{result.BacktestError}");
        Assert.True(result.NaiveError > result.BacktestError);
        Assert.Empty(result.Anomalies);
        Assert.All(result.Forecast, f => Assert.True(f.Low <= f.Estimate && f.Estimate <= f.High));
    }

    [Fact]
    public void Compute_WhenAPeriodIsFarFromTheTrend_ShouldMakeItStandOutWithWhatWasExpected()
    {
        double[] values = [100, 103, 98, 101, 99, 102, 400, 100, 98, 103, 101, 99];

        var result = TrendCalculator.Compute(Monthly(values), "date", "amount", "sum", "month", 2).Value!;

        var anomaly = Assert.Single(result.Anomalies);
        Assert.Equal(("2025-07", 400.0), (anomaly.Period, anomaly.Value));
        Assert.InRange(anomaly.Expected, 85, 120);
        Assert.True(anomaly.Deviations > 3.5);
    }

    [Fact]
    public void Compute_WhenAMonthHasNoRowsOrTheSeriesIsShort_ShouldCountZeroAndNotForecast()
    {
        var table = Csv.Check("date,amount\n2025-01-03,5\n2025-03-09,7\n");

        var result = TrendCalculator.Compute(table, "date", "amount", "sum", "month", 1).Value!;

        Assert.Equal([("2025-01", 5.0), ("2025-02", 0.0), ("2025-03", 7.0)], result.Series.Select(p => (p.Period, p.Value)));
        Assert.Empty(result.Forecast);
        Assert.Contains("at least 6", result.Note);
        Assert.Contains("sum, count or average", TrendCalculator.Compute(table, "date", "amount", "max", "month", 1).Refusal);
        Assert.Contains("between 1 and 12", TrendCalculator.Compute(table, "date", "amount", "sum", "month", 0).Refusal);
    }
}
