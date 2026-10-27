using Filum.Evals.LongMemEval;
using Filum.Evals.UnitTests.Builders;

namespace Filum.Evals.UnitTests.LongMemEval;

public sealed class LongMemEvalSubsetTests
{
    [Fact]
    public async Task Build_WhenGivenASeed_ShouldTakeTheSameNumberPerAbilityAndTheSameIdsForTheSameSeed()
    {
        var three = await LongMemEvalInstances.LoadAsync();
        var many = Enumerable.Range(0, 40).Select(n => three[n % 3] with { QuestionId = $"q{n}{(n % 3 == 1 ? "_abs" : "")}" }).ToList();

        var first = LongMemEvalSubset.Build(many, perAbility: 4, seed: 7);
        var again = LongMemEvalSubset.Build(many, perAbility: 4, seed: 7);
        var other = LongMemEvalSubset.Build(many, perAbility: 4, seed: 8);

        Assert.Equal(12, first.QuestionIds.Count);
        Assert.All(many.Where(i => first.QuestionIds.Contains(i.QuestionId)).GroupBy(i => i.Ability), g => Assert.Equal(4, g.Count()));
        Assert.Equal(first.QuestionIds, again.QuestionIds);
        Assert.NotEqual(first.QuestionIds, other.QuestionIds);
    }
}
