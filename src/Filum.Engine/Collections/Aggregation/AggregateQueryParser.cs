using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Collections.Periods;

namespace Filum.Engine.Collections.Aggregation;

// Every refusal says what to send instead.
internal static class AggregateQueryParser
{
    private const int UnknownColumn = -1;

    public static MemoryOutcome<AggregateQuery> Parse(CsvTable table, AggregateRequest request)
    {
        var name = request.Operation?.Trim().ToLowerInvariant();
        var operation = name is null ? null : AggregateStrategies.Find(name);
        if (operation is null)
        {
            return Refused($"The operation must be {Choices(CollectionAggregator.Operations)}.");
        }

        if (operation.NeedsColumn && string.IsNullOrWhiteSpace(request.Column))
        {
            return Refused($"The operation {operation.Name} needs a column.");
        }

        var periodName = request.Period?.Trim().ToLowerInvariant();
        var period = periodName is null ? null : CalendarPeriods.Find(periodName);
        if (periodName is not null && period is null)
        {
            return Refused($"The period must be {Choices(CollectionAggregator.Periods)}.");
        }

        return CheckFilters(request, out var from, out var to) is { } refusal
            ? Refused(refusal)
            : Columns(table, request, new AggregateQuery(operation, period, from, to, request.FilterValue, null, null, null, null));
    }

    private static string? CheckFilters(AggregateRequest request, out DateOnly? from, out DateOnly? to)
    {
        from = null;
        to = null;
        if (request.Period is not null && string.IsNullOrWhiteSpace(request.GroupBy))
        {
            return "A period needs groupBy: the date column to group by.";
        }

        if (string.IsNullOrWhiteSpace(request.FilterColumn) != (request.FilterValue is null))
        {
            return "filterColumn and filterValue go together.";
        }

        if ((request.From is not null || request.To is not null) && string.IsNullOrWhiteSpace(request.DateColumn))
        {
            return "from and to need dateColumn: the date column to filter on.";
        }

        if (request.From is not null && (from = CollectionValues.ParseDate(request.From)) is null)
        {
            return $"from must be a date written as yyyy-MM-dd, not '{request.From}'.";
        }

        if (request.To is not null && (to = CollectionValues.ParseDate(request.To)) is null)
        {
            return $"to must be a date written as yyyy-MM-dd, not '{request.To}'.";
        }

        return null;
    }

    private static MemoryOutcome<AggregateQuery> Columns(CsvTable table, AggregateRequest request, AggregateQuery query)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Header.Count; i++)
        {
            columns[table.Header[i]] = i;
        }

        int? Index(string? name) => string.IsNullOrWhiteSpace(name) ? null : columns.TryGetValue(name.Trim(), out var index) ? index : UnknownColumn;
        var resolved = query with
        {
            ValueIndex = Index(request.Column),
            GroupIndex = Index(request.GroupBy),
            FilterIndex = Index(request.FilterColumn),
            DateIndex = Index(request.DateColumn)
        };
        foreach (var (name, index) in new[] { (request.Column, resolved.ValueIndex), (request.GroupBy, resolved.GroupIndex), (request.FilterColumn, resolved.FilterIndex), (request.DateColumn, resolved.DateIndex) })
        {
            if (index == UnknownColumn)
            {
                return Refused($"The collection has no field '{name?.Trim()}'; its fields are {string.Join(", ", table.Header)}.");
            }
        }

        return MemoryOutcome<AggregateQuery>.Ok(resolved);
    }

    private static string Choices(IReadOnlyList<string> names) => $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}";

    private static MemoryOutcome<AggregateQuery> Refused(string reason) => MemoryOutcome<AggregateQuery>.Refused(reason);
}
