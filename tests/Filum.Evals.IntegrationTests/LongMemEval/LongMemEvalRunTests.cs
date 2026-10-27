using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.IntegrationTests.LongMemEval;

public sealed class LongMemEvalRunTests
{
    [Fact]
    public async Task RunAsync_WhenGivenAJournal_ShouldWriteEveryAnswerAtOnceAndResumeWithoutPlayingThemAgain()
    {
        var instances = await LongMemEvalInstances.LoadAsync();
        var journal = Path.Combine(Path.GetTempPath(), $"lme-journal-{Guid.NewGuid():N}.jsonl");
        var judge = new SayJudge();
        try
        {
            var stopped = await new LongMemEvalRun(judge, capUsd: 10, maxSessions: 4, journal: journal).RunAsync(instances, [new FixedSystem("x", sessions: 2)], 1, CancellationToken.None);
            Assert.Equal(2, stopped.Results.Count);
            Assert.Equal(2, File.ReadAllLines(journal).Length);

            var resumed = await new LongMemEvalRun(judge, capUsd: 10, journal: journal).RunAsync(instances, [new FixedSystem("x", sessions: 2)], 1, CancellationToken.None);

            Assert.Equal(3, resumed.Results.Count);
            Assert.Equal(3, judge.Prompts.Count);
            Assert.Equal(instances.Select(i => i.QuestionId), resumed.Results.Select(r => r.QuestionId));
        }
        finally
        {
            File.Delete(journal);
        }
    }
}
