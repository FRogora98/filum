using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

// In the order requests list them; a new operation is a new IAggregateStrategy.
internal static class AggregateStrategies
{
    public static readonly IReadOnlyList<IAggregateStrategy> All =
        [new CountAggregateStrategy(), new SumAggregateStrategy(), new AverageAggregateStrategy(), new MinAggregateStrategy(), new MaxAggregateStrategy()];

    public static IAggregateStrategy? Find(string name) => All.FirstOrDefault(o => o.Name == name);
}
