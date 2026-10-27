using Filum.Evals.LongMemEval.Systems;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.UnitTests.LongMemEval.Systems;

public sealed class NaiveRetrievalSystemStrategyTests
{
    [Fact]
    public async Task AnswerAsync_WhenAReferenceAnswers_ShouldAskTheSameQuestionWithNothingOrWithTheBestSessions()
    {
        var instance = (await LongMemEvalInstances.LoadAsync())[2];
        var fake = new RecordingClient();
        var model = new DirectModel("small", fake, 1m, 2m);

        await new NoMemorySystemStrategy(model).AnswerAsync(instance, CancellationToken.None);
        await new NaiveRetrievalSystemStrategy(model, k: 1).AnswerAsync(instance, CancellationToken.None);

        Assert.Equal("Today is 2023/06/01 (Thu) 09:00. How many plants do I have now?", fake.Prompts[0]);
        // BM25 picks the older session ("I have 3 plants" matches more words) and misses the update: the very weakness of
        // naive retrieval on knowledge updates that the benchmark is there to show.
        Assert.Contains("[Conversation on 2023/04/01 (Sat) 08:00]", fake.Prompts[1]);
        Assert.DoesNotContain("2023/05/01 (Mon) 08:00", fake.Prompts[1]);
        Assert.EndsWith("Today is 2023/06/01 (Thu) 09:00. How many plants do I have now?", fake.Prompts[1]);
    }
}
