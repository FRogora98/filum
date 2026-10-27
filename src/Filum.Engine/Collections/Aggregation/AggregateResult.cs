namespace Filum.Engine.Collections.Aggregation;

internal sealed record AggregateResult(string Operation, string? Column, IReadOnlyList<AggregateGroup> Groups, int RowsUsed, int RowsSkipped);
