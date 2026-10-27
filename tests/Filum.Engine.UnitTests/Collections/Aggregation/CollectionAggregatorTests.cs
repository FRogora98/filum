using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Collections;
using Filum.Engine.Collections.Aggregation;

namespace Filum.Engine.UnitTests.Collections.Aggregation;

public sealed class CollectionAggregatorTests
{
    // A neutral synthetic collection: what was read, when, how many pages, and a few values that cannot be read.
    private static readonly CsvTable Table = Csv.Check("""
        date,kind,pages,note
        2026-08-30,novel,120.5,
        2026-08-31,essay,80,
        2026-09-01,novel,200,
        2026-09-01T21:30,essay,40.25,
        2026-09-15,novel,1.000,thousands separator? no: one
        2026-09-20,novel,n/a,unreadable number
        not a date,essay,10,unreadable date
        2026-10-02,Novel,-5,a correction
        """);

    [Fact]
    public void Compute_WhenEveryOperationRunsOverAColumn_ShouldBeExact()
    {
        Assert.Equal(120.5m + 80 + 200 + 40.25m + 1 + 10 - 5, Value(new AggregateRequest("sum", "pages")));
        Assert.Equal(-5m, Value(new AggregateRequest("min", "pages")));
        Assert.Equal(200m, Value(new AggregateRequest("max", "pages")));
        Assert.Equal(Math.Round((120.5m + 80 + 200 + 40.25m + 1 + 10 - 5) / 7, 6, MidpointRounding.AwayFromZero), Value(new AggregateRequest("average", "pages")));
        Assert.Equal(8m, Value(new AggregateRequest("count")));
        Assert.Equal(4m, Value(new AggregateRequest("count", "note")));
    }

    [Fact]
    public void Compute_WhenValuesAreUnreadable_ShouldSkipAndCountThem()
    {
        var result = Ok(new AggregateRequest("sum", "pages"));

        Assert.Equal((7, 1), (result.RowsUsed, result.RowsSkipped));
    }

    [Fact]
    public void Compute_WhenGroupedByAColumnOrAMonth_ShouldGroupThem()
    {
        var byKind = Ok(new AggregateRequest("sum", "pages", GroupBy: "kind")).Groups;
        Assert.Equal([("essay", 130.25m, 3), ("novel", 316.5m, 4)], byKind.Select(g => (g.Key!, g.Value, g.Rows)));

        var byMonth = Ok(new AggregateRequest("sum", "pages", GroupBy: "date", Period: "month"));
        Assert.Equal([("2026-08", 200.5m), ("2026-09", 241.25m), ("2026-10", -5m)], byMonth.Groups.Select(g => (g.Key!, g.Value)));
        Assert.Equal(2, byMonth.RowsSkipped);
    }

    [Fact]
    public void Compute_WhenGroupedByDayIsoWeekOrYear_ShouldGroupThem()
    {
        Assert.Equal(["2026-08-30", "2026-08-31", "2026-09-01", "2026-09-15", "2026-09-20", "2026-10-02"],
            Ok(new AggregateRequest("count", GroupBy: "date", Period: "day")).Groups.Select(g => g.Key));
        Assert.Equal([("2026-W35", 1m), ("2026-W36", 3m), ("2026-W38", 2m), ("2026-W40", 1m)],
            Ok(new AggregateRequest("count", GroupBy: "date", Period: "week")).Groups.Select(g => (g.Key!, g.Value)));
        Assert.Equal([("2026", 7m)], Ok(new AggregateRequest("count", GroupBy: "date", Period: "year")).Groups.Select(g => (g.Key!, g.Value)));
    }

    [Fact]
    public void Compute_WhenFilteredByAValueAndADateRange_ShouldKeepTheMatchingRows()
    {
        Assert.Equal(120.5m + 200 + 1 - 5, Value(new AggregateRequest("sum", "pages", FilterColumn: "kind", FilterValue: "NOVEL")));
        Assert.Equal(200m + 40.25m + 1, Value(new AggregateRequest("sum", "pages", DateColumn: "date", From: "2026-09-01", To: "2026-09-30")));
        Assert.Equal(-5m, Value(new AggregateRequest("sum", "pages", DateColumn: "date", From: "2026-10-01")));
    }

    [Fact]
    public void Compute_WhenTheFilterMatchesNothing_ShouldGiveNoGroups()
    {
        var result = Ok(new AggregateRequest("sum", "pages", FilterColumn: "kind", FilterValue: "poem"));

        Assert.Empty(result.Groups);
        Assert.Equal(0, result.RowsUsed);
    }

    [Theory]
    [InlineData("median", "pages", null, null, "must be count, sum")]
    [InlineData("sum", null, null, null, "needs a column")]
    [InlineData("sum", "weight", null, null, "no field 'weight'; its fields are date, kind, pages, note")]
    [InlineData("sum", "pages", "date", "quarter", "day, week, month or year")]
    [InlineData("count", null, null, "month", "needs groupBy")]
    public void Compute_WhenTheRequestIsWrong_ShouldRefuseIt(string operation, string? column, string? groupBy, string? period, string reason)
    {
        var outcome = CollectionAggregator.Compute(Table, new AggregateRequest(operation, column, groupBy, period));

        Assert.True(outcome.IsRefused);
        Assert.Contains(reason, outcome.Refusal);
    }

    [Fact]
    public void Compute_WhenAFilterIsWrong_ShouldRefuseIt()
    {
        Assert.Contains("go together", CollectionAggregator.Compute(Table, new AggregateRequest("count", FilterColumn: "kind")).Refusal);
        Assert.Contains("need dateColumn", CollectionAggregator.Compute(Table, new AggregateRequest("count", From: "2026-01-01")).Refusal);
        Assert.Contains("yyyy-MM-dd", CollectionAggregator.Compute(Table, new AggregateRequest("count", DateColumn: "date", From: "01/09/2026")).Refusal);
    }

    private static AggregateResult Ok(AggregateRequest request)
    {
        Assert.True(Table.IsValid, Table.Error);
        var outcome = CollectionAggregator.Compute(Table, request);
        Assert.False(outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }

    private static decimal Value(AggregateRequest request) => Assert.Single(Ok(request).Groups).Value;
}
