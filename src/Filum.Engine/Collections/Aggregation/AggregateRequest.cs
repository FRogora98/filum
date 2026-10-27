namespace Filum.Engine.Collections.Aggregation;

internal sealed record AggregateRequest(
    string? Operation,
    string? Column = null,
    string? GroupBy = null,
    string? Period = null,
    string? FilterColumn = null,
    string? FilterValue = null,
    string? DateColumn = null,
    string? From = null,
    string? To = null);
