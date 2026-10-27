using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalRunTests
{
    [Fact]
    public async Task RunAsync_WhenGivenCaps_ShouldJudgeEveryAnswerReportAccuracyOverRepetitionsAndStopAtThem()
    {
        var instances = await LongMemEvalInstances.LoadAsync();
        var judge = new SayJudge();
        var system = new FixedSystem("x", sessions: 2);

        var run = await new LongMemEvalRun(judge, capUsd: 10).RunAsync(instances, [system], repetitions: 2, CancellationToken.None);
        var score = Assert.Single(LongMemEvalScore.Of(run));

        Assert.Equal(6, run.Results.Count);
        Assert.Equal(6, judge.Prompts.Count);
        Assert.Equal((3, 2), (score.Questions, score.Repetitions));
        Assert.Equal(1.0 / 3, score.Accuracy, 3);
        Assert.Equal(1.0, score.ByAbility[LongMemEvalAbilities.Temporal]);
        Assert.Contains("| x | fixed | 3 × 2 |", LongMemEvalReport.Markdown(run, TimeProvider.System));

        var bySessions = await new LongMemEvalRun(judge, capUsd: 10, maxSessions: 3).RunAsync(instances, [system], 1, CancellationToken.None);
        Assert.True(bySessions.Stopped);
        Assert.Single(bySessions.Results);

        var byDollars = await new LongMemEvalRun(judge, capUsd: 0.000001m).RunAsync(instances, [system], 1, CancellationToken.None);
        Assert.True(byDollars.Stopped);
        Assert.Single(byDollars.Results);
    }
}
