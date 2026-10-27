using System.Globalization;
using System.Text;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Collections;
using Filum.Engine.Collections.Trends;
using Filum.Engine.IntegrationTests.Snapshots;
using static Filum.Engine.IntegrationTests.Snapshots.SnapshotText;

namespace Filum.Engine.IntegrationTests.Collections.Trends;

// Pinned in a snapshot: a forecast that changes in the last digit changes an answer.
public sealed class TrendCalculatorTests
{
    [Fact]
    public void Trends_WhenComputingSeries_ShouldMatchTheirSnapshot()
    {
        var text = new StringBuilder();
        foreach (var (name, table, operation, period, horizon) in TrendSamples())
        {
            text.Append("## ").Append(name).Append(' ').Append(operation ?? "null").Append(' ').Append(period ?? "null").Append(' ')
                .Append(horizon.ToString(CultureInfo.InvariantCulture)).Append('\n');
            var outcome = TrendCalculator.Compute(table, "date", "amount", operation, period, horizon);
            if (outcome.IsRefused)
            {
                text.Append("refused: ").Append(Escape(outcome.Refusal!)).Append('\n');
                continue;
            }

            var r = outcome.Value!;
            text.Append(r.Operation).Append(' ').Append(r.Column).Append(' ').Append(r.Period).Append(" backtest=").Append(Number(r.BacktestError))
                .Append(" naive=").Append(Number(r.NaiveError)).Append(" note=").Append(Escape(r.Note ?? "null")).Append('\n');
            text.Append("series ").AppendJoin(' ', r.Series.Select(p => $"{p.Period}:{Number(p.Value)}")).Append('\n');
            foreach (var f in r.Forecast)
            {
                text.Append("forecast ").Append(f.Period).Append(' ').Append(Number(f.Estimate)).Append(' ').Append(Number(f.Low)).Append(' ').Append(Number(f.High)).Append('\n');
            }

            foreach (var a in r.Anomalies)
            {
                text.Append("anomaly ").Append(a.Period).Append(' ').Append(Number(a.Value)).Append(' ').Append(Number(a.Expected)).Append(' ').Append(Number(a.Deviations)).Append('\n');
            }
        }

        SnapshotFile.Match("Trends.snapshot.txt", text.ToString());
    }

    private static CsvTable Series(string period, params double[] values) =>
        Csv.Check("date,amount\n" + string.Concat(values.Select((v, i) =>
        {
            var start = new DateOnly(2024, 12, 30);
            var date = period switch
            {
                "day" => start.AddDays(i),
                "week" => start.AddDays(7 * i),
                "year" => start.AddYears(i),
                _ => start.AddMonths(i)
            };
            return $"{date:yyyy-MM-dd},{v.ToString(CultureInfo.InvariantCulture)}\n";
        })));

    private static IEnumerable<(string Name, CsvTable Table, string? Operation, string? Period, int Horizon)> TrendSamples()
    {
        double[] rise = [.. Enumerable.Range(0, 12).Select(i => 100.0 + 10 * i)];
        double[] spike = [100, 103, 98, 101, 99, 102, 400, 100, 98, 103, 101, 99];
        double[] noisy = [5.5, 7.25, 3, 9, 4.75, 8, 6, 10.5, 2, 7, 6.5, 9.25, 1, 12];
        yield return ("rise", Series("month", rise), "sum", "month", 3);
        yield return ("rise", Series("month", rise), null, null, 12);
        yield return ("spike", Series("month", spike), "sum", "month", 2);
        yield return ("spike", Series("month", spike), " Average ", "month", 4);
        yield return ("noisy", Series("day", noisy), "sum", "day", 5);
        yield return ("noisy", Series("week", noisy), "count", "week", 3);
        yield return ("noisy", Series("week", noisy), "sum", "week", 6);
        yield return ("noisy", Series("year", noisy), "average", "year", 2);
        yield return ("gaps", Csv.Check("date,amount\n2025-01-03,5\n2025-03-09,7\n2025-06-01,2\n2025-09-09,4\n2025-12-01,1\n2026-02-02,8\n"), "sum", "month", 2);
        yield return ("gaps", Csv.Check("date,amount\n2025-01-03,5\n2025-03-09,7\n2025-06-01,2\n2025-09-09,4\n2025-12-01,1\n2026-02-02,8\n"), "average", "month", 2);
        yield return ("short", Csv.Check("date,amount\n2025-01-03,5\n2025-03-09,7\n"), "sum", "month", 1);
        yield return ("flat", Series("month", 4, 4, 4, 4, 4, 4, 4, 4, 4), "sum", "month", 1);
        yield return ("refused", Series("month", rise), "max", "month", 1);
        yield return ("refused", Series("month", rise), "sum", "month", 0);
        yield return ("refused", Series("month", rise), "sum", "month", 13);
        yield return ("refused", Series("month", rise), "sum", "decade", 1);
    }
}
