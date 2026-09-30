using System.Globalization;

namespace Filum.Engine;

public sealed record AggregateRequest(
    string? Operation,
    string? Column = null,
    string? GroupBy = null,
    string? Period = null,
    string? FilterColumn = null,
    string? FilterValue = null,
    string? DateColumn = null,
    string? From = null,
    string? To = null);

/// <summary>One group of the result; the key is null when nothing is grouped.</summary>
public sealed record AggregateGroup(string? Key, decimal Value, int Rows);

public sealed record AggregateResult(string Operation, string? Column, IReadOnlyList<AggregateGroup> Groups, int RowsUsed, int RowsSkipped);

/// <summary>
/// Counts, sums, averages, minimums and maximums over a collection, in code and never by the model. Numbers are read
/// in invariant culture (<c>.</c> as decimal separator, optional sign, no thousands separator), dates as
/// <c>yyyy-MM-dd</c> optionally followed by a time. A value that cannot be read is skipped and counted, never guessed.
/// </summary>
public static class CollectionAggregator
{
    public static readonly IReadOnlyList<string> Operations = ["count", "sum", "average", "min", "max"];
    public static readonly IReadOnlyList<string> Periods = ["day", "week", "month", "year"];

    public static MemoryOutcome<AggregateResult> Compute(CsvTable table, AggregateRequest request)
    {
        var operation = request.Operation?.Trim().ToLowerInvariant();
        if (operation is null || !Operations.Contains(operation))
        {
            return Refused("The operation must be count, sum, average, min or max.");
        }

        if (operation != "count" && string.IsNullOrWhiteSpace(request.Column))
        {
            return Refused($"The operation {operation} needs a column.");
        }

        var period = request.Period?.Trim().ToLowerInvariant();
        if (period is not null && !Periods.Contains(period))
        {
            return Refused("The period must be day, week, month or year.");
        }

        if (period is not null && string.IsNullOrWhiteSpace(request.GroupBy))
        {
            return Refused("A period needs groupBy: the date column to group by.");
        }

        if (string.IsNullOrWhiteSpace(request.FilterColumn) != (request.FilterValue is null))
        {
            return Refused("filterColumn and filterValue go together.");
        }

        if ((request.From is not null || request.To is not null) && string.IsNullOrWhiteSpace(request.DateColumn))
        {
            return Refused("from and to need dateColumn: the date column to filter on.");
        }

        DateOnly? from = null, to = null;
        if (request.From is not null && (from = ParseDate(request.From)) is null)
        {
            return Refused($"from must be a date written as yyyy-MM-dd, not '{request.From}'.");
        }

        if (request.To is not null && (to = ParseDate(request.To)) is null)
        {
            return Refused($"to must be a date written as yyyy-MM-dd, not '{request.To}'.");
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Header.Count; i++)
        {
            columns[table.Header[i]] = i;
        }

        int? Index(string? name) => string.IsNullOrWhiteSpace(name) ? null : columns.TryGetValue(name.Trim(), out var index) ? index : -1;
        var valueIndex = Index(request.Column);
        var groupIndex = Index(request.GroupBy);
        var filterIndex = Index(request.FilterColumn);
        var dateIndex = Index(request.DateColumn);
        foreach (var (name, index) in new[] { (request.Column, valueIndex), (request.GroupBy, groupIndex), (request.FilterColumn, filterIndex), (request.DateColumn, dateIndex) })
        {
            if (index == -1)
            {
                return Refused($"The collection has no field '{name!.Trim()}'; its fields are {string.Join(", ", table.Header)}.");
            }
        }

        // Grouping ignores case like the filter does; a group keeps the spelling it was first seen with.
        var groups = new SortedDictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        var used = 0;
        var skipped = 0;
        foreach (var row in table.Rows)
        {
            string Field(int index) => row.Fields[index].Trim();

            if (filterIndex is { } f && !string.Equals(Field(f), request.FilterValue!.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (dateIndex is { } d)
            {
                var date = ParseDate(Field(d));
                if (date is null)
                {
                    skipped++;
                    continue;
                }

                if (date < from || date > to)
                {
                    continue;
                }
            }

            var key = string.Empty;
            if (groupIndex is { } g)
            {
                if (period is null)
                {
                    key = Field(g);
                }
                else if (ParseDate(Field(g)) is { } date)
                {
                    key = Bucket(date, period);
                }
                else
                {
                    skipped++;
                    continue;
                }
            }

            decimal value = 1;
            if (valueIndex is { } v)
            {
                var text = Field(v);
                if (operation == "count" ? text.Length == 0 : !TryParseNumber(text, out value))
                {
                    skipped++;
                    continue;
                }
            }

            if (!groups.TryGetValue(key, out var values))
            {
                groups[key] = values = [];
            }

            values.Add(value);
            used++;
        }

        var result = groups
            .Select(g => new AggregateGroup(groupIndex is null ? null : g.Key, Apply(operation, g.Value), g.Value.Count))
            .ToList();
        return MemoryOutcome<AggregateResult>.Ok(new AggregateResult(operation, request.Column?.Trim(), result, used, skipped));
    }

    private static decimal Apply(string operation, List<decimal> values) => operation switch
    {
        "count" => values.Count,
        "sum" => values.Sum(),
        "average" => Math.Round(values.Sum() / values.Count, 6, MidpointRounding.AwayFromZero),
        "min" => values.Min(),
        _ => values.Max()
    };

    private static string Bucket(DateOnly date, string period) => period switch
    {
        "day" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "week" => $"{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue))}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):00}",
        "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy", CultureInfo.InvariantCulture)
    };

    private static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static DateOnly? ParseDate(string text)
    {
        text = text.Trim();
        if (text.Length > 10 && text[10] is 'T' or ' ')
        {
            text = text[..10];
        }

        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private static MemoryOutcome<AggregateResult> Refused(string reason) => MemoryOutcome<AggregateResult>.Refused(reason);
}
