using Filum.Engine.Abstractions.Collections;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Collections.Periods;

namespace Filum.Engine.Collections.Aggregation;

// In code, never by the model: a value that cannot be read is skipped and counted, never guessed.
internal static class CollectionAggregator
{
    public static readonly IReadOnlyList<string> Operations = AggregateStrategies.All.Select(o => o.Name).ToList();
    public static readonly IReadOnlyList<string> Periods = CalendarPeriods.All.Select(p => p.Name).ToList();

    public static MemoryOutcome<AggregateResult> Compute(CsvTable table, AggregateRequest request)
    {
        var parsed = AggregateQueryParser.Parse(table, request);
        if (parsed.IsRefused)
        {
            return MemoryOutcome<AggregateResult>.Refused(parsed.Refusal);
        }

        var query = parsed.Value;
        var scan = new AggregateRowScan(query).Read(table.Rows);
        var groups = scan.Groups
            .Select(g => new AggregateGroup(query.GroupIndex is null ? null : g.Key, query.Operation.Apply(g.Value), g.Value.Count))
            .ToList();
        return MemoryOutcome<AggregateResult>.Ok(new AggregateResult(query.Operation.Name, request.Column?.Trim(), groups, scan.Used, scan.Skipped));
    }
}
