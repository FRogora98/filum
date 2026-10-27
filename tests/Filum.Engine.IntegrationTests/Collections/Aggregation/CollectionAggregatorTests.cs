using System.Globalization;
using System.Text;
using Filum.Engine.Collections;
using Filum.Engine.Collections.Aggregation;
using Filum.Engine.IntegrationTests.Snapshots;
using static Filum.Engine.IntegrationTests.Snapshots.SnapshotText;

namespace Filum.Engine.IntegrationTests.Collections.Aggregation;

// Pinned in a snapshot: an aggregate that changes in the last digit changes an answer.
public sealed class CollectionAggregatorTests
{
    [Fact]
    public void CollectionAggregator_WhenComputingEveryOperation_ShouldMatchItsSnapshot()
    {
        var table = Csv.Check(AggregateTable);
        var text = new StringBuilder();
        foreach (var request in AggregateRequests())
        {
            text.Append("## ").Append(request).Append('\n');
            var outcome = CollectionAggregator.Compute(table, request);
            if (outcome.IsRefused)
            {
                text.Append("refused: ").Append(Escape(outcome.Refusal!)).Append('\n');
                continue;
            }

            var result = outcome.Value!;
            text.Append(result.Operation).Append(' ').Append(result.Column ?? "null").Append(" used=").Append(result.RowsUsed.ToString(CultureInfo.InvariantCulture))
                .Append(" skipped=").Append(result.RowsSkipped.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var group in result.Groups)
            {
                text.Append(Escape(group.Key ?? "null")).Append(" = ").Append(group.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(" rows=").Append(group.Rows.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        SnapshotFile.Match("CollectionAggregator.snapshot.txt", text.ToString());
    }

    private const string AggregateTable = """
        date,category,amount,note
        2024-12-30,Food,10.50,a
        2024-12-31,food,4.25,b
        2025-01-01,Travel,100,c
        2025-01-01T08:00:00,Travel,-20.125,d
        2025-01-05 09:00,Food,,e
        2025-01-06,Food,abc,f
        bad-date,Food,3,g
        2025-02-01,Home,1000000.000001,h
        2025-02-28,Home,0.3333333,i
        2025-03-15,,7,j
        2026-01-01,Food,1e3,k
        2025-03-16,Food,1.000,
        """;

    private static IEnumerable<AggregateRequest> AggregateRequests()
    {
        foreach (var operation in new[] { "count", "sum", "average", "min", "max", " SUM " })
        {
            yield return new AggregateRequest(operation, operation.Trim().Equals("count", StringComparison.OrdinalIgnoreCase) ? null : "amount");
            yield return new AggregateRequest(operation, "amount", GroupBy: "category");
            foreach (var period in new[] { "day", "week", "month", "year" })
            {
                yield return new AggregateRequest(operation, "amount", GroupBy: "date", Period: period);
            }

            yield return new AggregateRequest(operation, "amount", FilterColumn: "category", FilterValue: " FOOD ");
            yield return new AggregateRequest(operation, "amount", GroupBy: "category", DateColumn: "date", From: "2025-01-01", To: "2025-02-28");
        }

        yield return new AggregateRequest("count", "note", GroupBy: "category");
        yield return new AggregateRequest("count", GroupBy: "date", Period: " Month ", From: "2025-01-01", DateColumn: "date");
        yield return new AggregateRequest(null);
        yield return new AggregateRequest("median", "amount");
        yield return new AggregateRequest("sum");
        yield return new AggregateRequest("sum", "amount", Period: "month");
        yield return new AggregateRequest("sum", "amount", GroupBy: "date", Period: "quarter");
        yield return new AggregateRequest("sum", "amount", FilterColumn: "category");
        yield return new AggregateRequest("sum", "amount", FilterValue: "x");
        yield return new AggregateRequest("sum", "amount", From: "2025-01-01");
        yield return new AggregateRequest("sum", "amount", DateColumn: "date", From: "01/01/2025");
        yield return new AggregateRequest("sum", "amount", DateColumn: "date", To: "tomorrow");
        yield return new AggregateRequest("sum", "price");
        yield return new AggregateRequest("sum", "amount", GroupBy: "kind");
    }
}
