using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text;
using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Collections.Aggregation;
using Filum.Engine.Collections.Trends;
using Filum.Engine.Memory.Contracts;
using Microsoft.Extensions.AI;
using static Filum.Engine.Tools.ToolText;

namespace Filum.Engine.Tools;

internal sealed class ComputeTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemoryReader Reader => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(Aggregate, ToolNames.CollectionAggregate,
            "Count, sum, average, min or max of a column, exactly, optionally grouped and filtered."),
        AIFunctionFactory.Create(ComputeRun, ToolNames.ComputeRun,
            "Run a JavaScript function body on collections, read-only: data[path] is an array of rows; return a value; print() adds output."),
        AIFunctionFactory.Create(Trend, ToolNames.CollectionTrend,
            "A column over time: a forecast, its error on the past, and what stands out. Estimates."),
    ];

    private Task<string> Aggregate(
        [Description("Path of the collection (.csv).")] string path,
        [Description("count, sum, average, min or max.")] string operation,
        [Description("Field to compute on.")] string? column = null,
        [Description("Field to group by.")] string? groupBy = null,
        [Description("day, week, month or year of groupBy.")] string? period = null,
        [Description("A field to filter on.")] string? filterColumn = null,
        [Description("Value filterColumn must have.")] string? filterValue = null,
        [Description("A date field to filter by range.")] string? dateColumn = null,
        [Description("First day included, yyyy-MM-dd.")] string? from = null,
        [Description("Last day included, yyyy-MM-dd.")] string? to = null,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.CollectionAggregate, path, "compute on", async () =>
        {
            var read = await Reader.ReadCollectionAsync(UserId, path, cancellationToken);
            if (read.Refuses(out var readRefusal, out var table))
            {
                return readRefusal;
            }

            if (!table.IsValid)
            {
                return $"{path} is not a valid collection: {table.Error}";
            }

            var outcome = CollectionAggregator.Compute(table, new AggregateRequest(operation, column, groupBy, period, filterColumn, filterValue, dateColumn, from, to));
            if (outcome.Refuses(out var refusal, out var result))
            {
                return refusal;
            }

            var lines = result.Groups.Count == 0
                ? "No rows match."
                : string.Join('\n', result.Groups.Select(g => $"{(g.Key is null ? result.Operation : g.Key)}: {g.Value.ToString(CultureInfo.InvariantCulture)} ({Plural(g.Rows, "row")})"));
            var skipped = result.RowsSkipped > 0 ? $", {result.RowsSkipped} skipped" : string.Empty;
            var skippedWhy = result.RowsSkipped > 0 ? $", {result.RowsSkipped} skipped because a value could not be read" : string.Empty;
            var of = result.Column is null ? "rows" : result.Column;
            var by = groupBy is null ? string.Empty : $" by {period ?? groupBy}";
            return new ToolDone(
                $"{lines}\n(computed, not estimated: {Plural(result.RowsUsed, "row")} used{skippedWhy})",
                ToolStep.Computed, path, $"Computed the {result.Operation} of {of}{by} on {path} · {Plural(result.RowsUsed, "row")}{skipped}");
        });

    private Task<string> Trend(
        [Description("Path of the collection (.csv).")] string path,
        [Description("Date field.")] string dateColumn,
        [Description("Number field; none counts rows.")] string? column = null,
        [Description("sum, count or average.")] string operation = "sum",
        [Description("day, week, month or year.")] string period = "month",
        [Description("Periods ahead.")] int horizon = 3,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.CollectionTrend, path, "compute the trend of", async () =>
        {
            var read = await Reader.ReadCollectionAsync(UserId, path, cancellationToken);
            if (read.Refuses(out var readRefusal, out var table))
            {
                return readRefusal;
            }

            if (!table.IsValid)
            {
                return $"{path} is not a valid collection: {table.Error}";
            }

            var outcome = TrendCalculator.Compute(table, dateColumn, column, operation, period, horizon);
            if (outcome.Refuses(out var refusal, out var t))
            {
                return refusal;
            }

            var of = $"the {t.Operation} of {t.Column ?? "rows"} by {t.Period}";
            if (t.Series.Count == 0)
            {
                return $"No row of {path} has a date in {dateColumn} written as yyyy-MM-dd: there is no series to look at.";
            }

            return new ToolDone(TrendText(t, of), ToolStep.Computed, path, $"Computed the trend of {of} on {path} · {Plural(t.Series.Count, t.Period)}");
        });

    private static string TrendText(TrendResult t, string of)
    {
        var text = new StringBuilder($"{char.ToUpperInvariant(of[0])}{of[1..]}: {Plural(t.Series.Count, t.Period)}, the last {t.Series[^1].Period} = {Number(t.Series[^1].Value)}.\n");
        if (t.Note is not null)
        {
            text.Append(t.Note);
            return text.ToString();
        }

        text.Append("Forecast (estimates, with the range they are likely in):\n");
        foreach (var f in t.Forecast)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {f.Period}: about {Number(f.Estimate)} ({Number(f.Low)} to {Number(f.High)})\n");
        }

        // A trend without a note has a forecast, so it has been backtested.
        var backtest = t.BacktestError ?? throw new UnreachableException();
        var naive = t.NaiveError ?? throw new UnreachableException();
        text.Append(CultureInfo.InvariantCulture, $"On its own past, this method was off by {Number(backtest)} on average (repeating the last value: {Number(naive)}).\n");
        text.Append(t.Anomalies.Count == 0
            ? "No period stands out."
            : "Standing out:\n" + string.Join("\n", t.Anomalies.Select(a => $"- {a.Period}: {Number(a.Value)}, where the trend expected about {Number(a.Expected)} ({Math.Abs(a.Deviations).ToString(CultureInfo.InvariantCulture)} times the usual deviation)")));
        return text.ToString();
    }

    private Task<string> ComputeRun(
        [Description("The body of a JavaScript function; it returns the result.")] string code,
        [Description("Collection paths to read, as data[path].")] List<string>? collections = null,
        [Description("Allow private collections.")] bool includePrivate = false,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.ComputeRun, collections is { Count: > 0 } ? collections[0] : null, "run code on", async () =>
        {
            var tables = new Dictionary<string, CsvTable>(StringComparer.Ordinal);
            foreach (var path in (collections ?? []).Distinct(StringComparer.Ordinal))
            {
                var file = await Reader.GetFileAsync(UserId, path, cancellationToken);
                if (file.Refuses(out var fileRefusal, out var found))
                {
                    return fileRefusal;
                }

                if (found.Info.Sensitivity == MemorySensitivity.Private && !includePrivate)
                {
                    return $"{path} is private: name it with includePrivate only when the person asks for it now.";
                }

                var read = await Reader.ReadCollectionAsync(UserId, path, cancellationToken);
                if (read.Refuses(out var readRefusal, out var table))
                {
                    return readRefusal;
                }

                if (!table.IsValid)
                {
                    return $"{path} is not a valid collection: {table.Error}";
                }

                tables[path] = table;
            }

            var result = Session.Sandbox.Run(code, tables, cancellationToken);
            if (result.Error is not null)
            {
                return result.Output.Length == 0 ? result.Error : $"{result.Error}\nPrinted before it stopped:\n{result.Output}";
            }

            var on = tables.Count == 0 ? "no collection" : string.Join(", ", tables.Keys);
            var data = JsonSerializer.SerializeToElement(new { code });
            return new ToolDone(
                $"Result (computed, not estimated): {result.Json}" + (result.Output.Length == 0 ? string.Empty : $"\nPrinted:\n{result.Output}"),
                ToolStep.Computed, tables.Keys.FirstOrDefault(), $"Ran code on {on} · {Plural(result.Rows, "row")}", Data: data);
        });
}
