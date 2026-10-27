using Filum.Engine.Collections.Periods;
using Filum.Engine.Collections.Aggregation.Contracts;

namespace Filum.Engine.Collections.Aggregation;

internal sealed record AggregateQuery(
    IAggregateStrategy Operation,
    ICalendarPeriodStrategy? Period,
    DateOnly? From,
    DateOnly? To,
    string? FilterValue,
    int? ValueIndex,
    int? GroupIndex,
    int? FilterIndex,
    int? DateIndex);
