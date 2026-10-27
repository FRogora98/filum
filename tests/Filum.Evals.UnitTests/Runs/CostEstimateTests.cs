using Filum.Evals.Hosts.Wire;
using Filum.Evals.Judging;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;

namespace Filum.Evals.UnitTests.Runs;

public sealed class CostEstimateTests
{
    [Fact]
    public void Of_WhenGivenScenariosModelsAndRepetitions_ShouldPriceEveryTurnAndItsJudge()
    {
        var scenario = ScenarioLoader.Parse("""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[]},{"chat":"a","message":"n","expect":[]}]}""", "t.json");
        var model = new ModelDto("a", "A", "", 1.5m, 6m, true);

        var estimate = CostEstimate.Of([scenario], [model], repetitions: 3, JudgeModel.Price);

        // 6 turns: 6 × (8,000 × 1.5 + 400 × 6) / 1M for the model, 6 × (600 × 0.75 + 20 × 4.5) / 1M for the judge.
        Assert.Equal(0.0864m + 0.00324m, estimate);
    }
}
