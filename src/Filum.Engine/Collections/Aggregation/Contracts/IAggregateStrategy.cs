namespace Filum.Engine.Collections.Aggregation.Contracts;

/// <summary>One operation of collection_aggregate: how it reads a field and what it makes of a group's values.</summary>
internal interface IAggregateStrategy
{
    /// <summary>The name a request gives, one of <see cref="CollectionAggregator.Operations"/>.</summary>
    string Name { get; }

    /// <summary>Whether the operation needs a column to read values from.</summary>
    bool NeedsColumn { get; }

    /// <summary>The value a row's field adds to its group; false when the row is skipped.</summary>
    bool TryRead(string field, out decimal value);

    /// <summary>The value of a group from the values of its rows (never empty).</summary>
    decimal Apply(IReadOnlyList<decimal> values);
}
