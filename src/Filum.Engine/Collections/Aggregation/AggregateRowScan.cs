using Filum.Engine.Abstractions.Collections;

namespace Filum.Engine.Collections.Aggregation;

// A row is left out by the filter or the date range, and skipped (and counted) when a date or value it needs cannot be
// read.
internal sealed class AggregateRowScan(AggregateQuery query)
{
    // Grouping ignores case like the filter does; a group keeps the spelling it was first seen with.
    public SortedDictionary<string, List<decimal>> Groups { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Used { get; private set; }

    public int Skipped { get; private set; }

    public AggregateRowScan Read(IEnumerable<CsvRecord> rows)
    {
        foreach (var row in rows)
        {
            Read(row);
        }

        return this;
    }

    private void Read(CsvRecord row)
    {
        string Field(int index) => row.Fields[index].Trim();

        if (query is { FilterIndex: { } f, FilterValue: { } wanted } && !string.Equals(Field(f), wanted.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (query.DateIndex is { } d && !InRange(Field(d)))
        {
            return;
        }

        if (!TryKey(Field, out var key) || !TryValue(Field, out var value))
        {
            Skipped++;
            return;
        }

        if (!Groups.TryGetValue(key, out var values))
        {
            Groups[key] = values = [];
        }

        values.Add(value);
        Used++;
    }

    private bool InRange(string field)
    {
        var date = CollectionValues.ParseDate(field);
        if (date is null)
        {
            Skipped++;
            return false;
        }

        return !(date < query.From || date > query.To);
    }

    private bool TryKey(Func<int, string> field, out string key)
    {
        key = string.Empty;
        if (query.GroupIndex is not { } g)
        {
            return true;
        }

        if (query.Period is null)
        {
            key = field(g);
            return true;
        }

        if (CollectionValues.ParseDate(field(g)) is { } date)
        {
            key = query.Period.Label(date.ToDateTime(TimeOnly.MinValue));
            return true;
        }

        return false;
    }

    private bool TryValue(Func<int, string> field, out decimal value)
    {
        value = 1;
        return query.ValueIndex is not { } v || query.Operation.TryRead(field(v), out value);
    }
}
