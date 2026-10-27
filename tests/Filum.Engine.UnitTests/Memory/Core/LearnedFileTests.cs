using Filum.Engine.Memory.Core;

namespace Filum.Engine.UnitTests.Memory.Core;

public sealed class LearnedFileTests
{
    [Fact]
    public void ForInstructions_WhenPastItsLimit_ShouldCarryTheNewestLinesAndCountTheRest()
    {
        var content = LearnedFile.Template + string.Concat(Enumerable.Range(1, 200).Select(i => $"- 2026-01-01: trait number {i} with some words to make it long\n"));

        var carried = LearnedFile.ForInstructions(content);

        Assert.True(carried.Length <= LearnedFile.MaxChars + 60);
        Assert.Contains("trait number 200", carried);
        Assert.DoesNotContain("trait number 1 ", carried);
        Assert.EndsWith("older lines in /learned.md)", carried);
    }
}
