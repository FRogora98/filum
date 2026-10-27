using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalDatasetTests
{
    [Fact]
    public async Task LoadAsync_WhenReadingTheBenchmarksFile_ShouldMapAbilitiesReadNumberAnswersAndPutSessionsInDateOrder()
    {
        var instances = await LongMemEvalInstances.LoadAsync();

        Assert.Equal([LongMemEvalAbilities.Temporal, LongMemEvalAbilities.Abstention, LongMemEvalAbilities.KnowledgeUpdate], instances.Select(i => i.Ability));
        Assert.Equal("4", instances[2].AnswerText);
        // The file has the boat's session first, at 17:50; the bike's, at 14:47, came first.
        Assert.Equal(["2023/04/10 (Mon) 14:47", "2023/04/10 (Mon) 17:50"], instances[0].Sessions.Select(s => s.Date));
        Assert.Contains("bike", instances[0].Sessions.First().Turns[0].Content);
    }
}
