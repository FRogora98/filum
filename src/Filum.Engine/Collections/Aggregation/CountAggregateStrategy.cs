using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

// With a column, only the rows where it has a value.
internal sealed class CountAggregateStrategy : IAggregateStrategy
{
    public string Name => "count";

    public bool NeedsColumn => false;

    public bool TryRead(string field, out decimal value)
    {
        value = 1;
        return field.Length > 0;
    }

    public decimal Apply(IReadOnlyList<decimal> values) => values.Count;
}
