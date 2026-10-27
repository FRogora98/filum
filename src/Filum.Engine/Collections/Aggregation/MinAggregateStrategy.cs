using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

internal sealed class MinAggregateStrategy : IAggregateStrategy
{
    public string Name => "min";

    public bool NeedsColumn => true;

    public bool TryRead(string field, out decimal value) => CollectionValues.TryParseNumber(field, out value);

    public decimal Apply(IReadOnlyList<decimal> values) => values.Min();
}
