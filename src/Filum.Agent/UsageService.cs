using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Filum.Agent;

public sealed record ModelUsageDto(string Model, string Name, int Messages, int InputTokens, int OutputTokens, decimal CostUsd);

public sealed record MonthlyUsageDto(string Month, decimal BudgetUsd, decimal SpentUsd, decimal RemainingUsd, IReadOnlyList<ModelUsageDto> ByModel);

/// <summary>What a user has spent in the current calendar month (UTC), read from the usage records, which outlive deleted conversations.</summary>
public sealed class UsageService(DbContext db, IOptions<UsageOptions> usageOptions, ModelCatalog modelCatalog)
{
    public decimal MonthlyBudgetUsd => usageOptions.Value.MonthlyBudgetUsd;

    public async Task<decimal> SpentThisMonthAsync(Guid userId, CancellationToken cancellationToken) =>
        await CurrentMonth(userId).SumAsync(u => u.CostUsd, cancellationToken);

    public async Task<MonthlyUsageDto> GetCurrentMonthAsync(Guid userId, CancellationToken cancellationToken)
    {
        var byModel = await CurrentMonth(userId)
            .GroupBy(u => u.Model)
            .Select(g => new
            {
                Model = g.Key,
                Messages = g.Count(),
                InputTokens = g.Sum(u => u.InputTokens),
                OutputTokens = g.Sum(u => u.OutputTokens),
                CostUsd = g.Sum(u => u.CostUsd)
            })
            .ToListAsync(cancellationToken);

        var spent = byModel.Sum(m => m.CostUsd);
        return new MonthlyUsageDto(
            MonthStart().ToString("yyyy-MM"),
            MonthlyBudgetUsd,
            spent,
            Math.Max(0m, MonthlyBudgetUsd - spent),
            byModel
                .OrderByDescending(m => m.CostUsd)
                .Select(m => new ModelUsageDto(m.Model, modelCatalog.Find(m.Model)?.Name ?? m.Model, m.Messages, m.InputTokens, m.OutputTokens, m.CostUsd))
                .ToList());
    }

    /// <summary>What the person spent since <paramref name="from"/>: messages, tokens and cost, for a host's own plans (spec 017).</summary>
    public async Task<UsageDto> GetSinceAsync(Guid userId, DateTimeOffset from, CancellationToken cancellationToken)
    {
        var records = db.Set<UsageRecord>().Where(u => u.UserId == userId && u.CreatedAt >= from);
        return new UsageDto(
            await records.SumAsync(u => u.InputTokens, cancellationToken),
            await records.SumAsync(u => u.OutputTokens, cancellationToken),
            await records.SumAsync(u => u.CostUsd, cancellationToken));
    }

    /// <summary>How many answers the person got since <paramref name="from"/>.</summary>
    public Task<int> MessagesSinceAsync(Guid userId, DateTimeOffset from, CancellationToken cancellationToken) =>
        db.Set<UsageRecord>().CountAsync(u => u.UserId == userId && u.CreatedAt >= from, cancellationToken);

    private IQueryable<UsageRecord> CurrentMonth(Guid userId)
    {
        var start = MonthStart();
        return db.Set<UsageRecord>().Where(u => u.UserId == userId && u.CreatedAt >= start);
    }

    private static DateTimeOffset MonthStart()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
