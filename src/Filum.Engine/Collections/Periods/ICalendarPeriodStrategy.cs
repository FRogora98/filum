using Filum.Engine.Collections.Aggregation;

namespace Filum.Engine.Collections.Periods;

/// <summary>A length of calendar time rows are grouped by (a day, a week…), with the label collection_aggregate gives it.</summary>
internal interface ICalendarPeriodStrategy
{
    /// <summary>The name a request gives, one of <see cref="CollectionAggregator.Periods"/>.</summary>
    string Name { get; }

    /// <summary>The label of the period that holds <paramref name="date"/>.</summary>
    string Label(DateTime date);

    /// <summary>The first day of the period with this label.</summary>
    DateTime Start(string label);

    /// <summary><paramref name="start"/> moved forward by <paramref name="count"/> periods.</summary>
    DateTime Advance(DateTime start, int count);
}
