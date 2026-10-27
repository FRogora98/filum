using Microsoft.Extensions.AI;

namespace Filum.Evals;

internal readonly record struct ModelPrice(decimal InputPerMillionUsd, decimal OutputPerMillionUsd)
{
    private const decimal TokensPerPrice = 1_000_000m;

    public decimal CostUsd(long inputTokens, long outputTokens) =>
        (inputTokens * InputPerMillionUsd + outputTokens * OutputPerMillionUsd) / TokensPerPrice;

    public decimal CostUsd(UsageDetails? usage) => CostUsd(usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0);
}
