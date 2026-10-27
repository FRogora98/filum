using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalEstimateTests
{
    [Fact]
    public async Task Of_WhenASystemsModelHasAPrice_ShouldPriceItsTokensAndJudgeEveryAnswer()
    {
        var instances = await LongMemEvalInstances.LoadAsync();
        var prices = new Dictionary<string, ModelPrice> { ["fixed"] = new(1m, 2m) };

        var estimate = LongMemEvalEstimate.Of(instances, [new FixedSystem("x", sessions: 0)], prices);

        // 3 questions × (300 × 1 + 300 × 2) / 1M, and 3 judged answers × (500 × 0.75 + 5 × 4.5) / 1M.
        Assert.Equal(0.0027m + 0.0011925m, estimate);
    }

    [Fact]
    public async Task Of_WhenASystemsModelHasNoPrice_ShouldCountOnlyTheJudge()
    {
        var instances = await LongMemEvalInstances.LoadAsync();

        var estimate = LongMemEvalEstimate.Of(instances, [new FixedSystem("x", sessions: 0)], new Dictionary<string, ModelPrice>());

        Assert.Equal(0.0011925m, estimate);
    }
}
