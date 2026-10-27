using Filum.Evals.Checks;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.UnitTests.Runs;

public sealed class TurnAssessorTests
{
    private static readonly ScenarioTurn Step = ScenarioLoader.Parse("""
        {"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[
          {"type":"judge","question":"Is it polite?","pass_if":"yes"},{"type":"no_write"}]}]}
        """, "t.json").Turns[0];

    private static TurnRecord Turn(string answer, string? error = null) => new(1, "a", "m", answer, [], 0, 0, 0m, 0.1, error);

    [Fact]
    public async Task AssessAsync_WhenTheAnswerClaimsAChangeNothingMade_ShouldAskTheJudgeTwiceAndCountTheClaim()
    {
        var judge = new ScriptedJudge { Yes = (question, _) => question == "Is it polite?" };

        var assessment = await new TurnAssessor(judge).AssessAsync(Step, Turn("I saved it, thank you."), new MemorySnapshot([]), [], CancellationToken.None);

        Assert.Equal([true, true], assessment.Checks.Select(c => c.Result.Passed));
        Assert.Equal("the judge said yes", assessment.Checks[0].Result.Detail);
        Assert.All(assessment.Checks, c => Assert.Equal(1, c.Turn));
        Assert.True(assessment.ClaimedNotDone);
        Assert.Equal(2 * ScriptedJudge.Cost, assessment.JudgeCostUsd);
    }

    [Fact]
    public async Task AssessAsync_WhenTheTurnFailed_ShouldFailEveryExpectationWithoutAskingTheJudge()
    {
        var judge = new ScriptedJudge();

        var assessment = await new TurnAssessor(judge).AssessAsync(Step, Turn(string.Empty, "502 down"), new MemorySnapshot([]), [], CancellationToken.None);

        Assert.All(assessment.Checks, c => Assert.Equal(("the turn failed: 502 down", false), (c.Result.Detail, c.Result.Passed)));
        Assert.Empty(judge.Questions);
        Assert.False(assessment.ClaimedNotDone);
        Assert.Equal(0m, assessment.JudgeCostUsd);
    }

    [Fact]
    public async Task AssessAsync_WhenThereIsNoAnswer_ShouldNotAskTheJudge()
    {
        var judge = new ScriptedJudge();

        var assessment = await new TurnAssessor(judge).AssessAsync(Step, Turn(string.Empty), new MemorySnapshot([]), [], CancellationToken.None);

        Assert.Equal("no answer to judge", assessment.Checks[0].Result.Detail);
        Assert.Empty(judge.Questions);
        Assert.Equal(0m, assessment.JudgeCostUsd);
    }
}
