using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalJudgeTests
{
    [Fact]
    public async Task Prompt_WhenGivenEachAbility_ShouldUseTheAuthorsPrompt()
    {
        var instances = await LongMemEvalInstances.LoadAsync();

        Assert.Contains("do not penalize off-by-one errors", LongMemEvalJudge.Prompt(instances[0], "the bike"));
        Assert.Contains("Does the model correctly identify the question as unanswerable?", LongMemEvalJudge.Prompt(instances[1], "I don't know"));
        Assert.Contains("updated answer is the required answer", LongMemEvalJudge.Prompt(instances[2], "4"));
        Assert.Contains("Correct Answer: 4", LongMemEvalJudge.Prompt(instances[2], "4"));
        Assert.Throws<NotSupportedException>(() => LongMemEvalJudge.Prompt(instances[2] with { QuestionType = "made-up" }, "x"));
    }
}
