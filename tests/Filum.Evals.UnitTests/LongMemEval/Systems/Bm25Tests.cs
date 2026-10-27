using Filum.Evals.LongMemEval.Systems;

namespace Filum.Evals.UnitTests.LongMemEval.Systems;

public sealed class Bm25Tests
{
    [Fact]
    public void Top_WhenASessionMatchesTheQuestion_ShouldRankItFirst()
    {
        var top = Bm25.Top(["We talked about the weather and the rain.", "I adopted a cat named Miso last week.", "The cat food is on sale."], "What is my cat's name?", 2);

        Assert.Equal(2, top.Count);
        Assert.Contains(1, top);
        Assert.DoesNotContain(0, top);
    }
}
