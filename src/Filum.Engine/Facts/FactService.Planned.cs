using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Facts;

// The values that do not hold yet (spec 055).
internal sealed partial class FactService
{
    /// <summary>The values that start after today, soonest first (spec 055).</summary>
    public async Task<MemoryOutcome<IReadOnlyList<Fact>>> PlannedAsync(Guid userId, string? subject, CancellationToken cancellationToken)
    {
        var all = await AllAsync(userId, cancellationToken);
        if (all.IsRefused)
        {
            return all;
        }

        var today = FactTable.Day(time.GetUtcNow());
        var names = string.IsNullOrWhiteSpace(subject) ? null : FactTable.Names(all.Value, subject);
        return MemoryOutcome<IReadOnlyList<Fact>>.Ok(all.Value
            .Where(f => !f.NeverHeld && string.CompareOrdinal(f.ValidFrom, today) > 0)
            .Where(f => names is null || names.Contains(f.Subject.Trim()))
            .OrderBy(f => f.ValidFrom, StringComparer.Ordinal)
            .ToList());
    }
}
