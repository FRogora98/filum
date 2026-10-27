using Microsoft.Extensions.AI;

namespace Filum.Evals.UnitTests;

public sealed class ModelPriceTests
{
    [Fact]
    public void CostUsd_WhenGivenTokens_ShouldChargeEachSideAtItsPricePerMillion()
    {
        var price = new ModelPrice(2m, 8m);

        Assert.Equal(6m, price.CostUsd(1_000_000, 500_000));
        Assert.Equal(0.0002m, price.CostUsd(100, 0));
        Assert.Equal(price.CostUsd(100, 5), price.CostUsd(new UsageDetails { InputTokenCount = 100, OutputTokenCount = 5 }));
        Assert.Equal(0m, price.CostUsd(null));
    }
}
