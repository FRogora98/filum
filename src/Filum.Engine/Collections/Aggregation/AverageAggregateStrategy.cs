using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

internal sealed class AverageAggregateStrategy : IAggregateStrategy
{
    private const int Decimals = 6;

    public string Name => "average";

    public bool NeedsColumn => true;

    public bool TryRead(string field, out decimal value) => CollectionValues.TryParseNumber(field, out value);

    public decimal Apply(IReadOnlyList<decimal> values) => Math.Round(values.Sum() / values.Count, Decimals, MidpointRounding.AwayFromZero);
}
