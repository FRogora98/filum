using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

internal sealed class SumAggregateStrategy : IAggregateStrategy
{
    public string Name => "sum";

    public bool NeedsColumn => true;

    public bool TryRead(string field, out decimal value) => CollectionValues.TryParseNumber(field, out value);

    public decimal Apply(IReadOnlyList<decimal> values) => values.Sum();
}
