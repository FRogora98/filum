using Filum.Mcp.Hook;

namespace Filum.Mcp.UnitTests.Hook;

public sealed class TextPartsTests
{
    private const int Max = McpConstants.Limits.MaxText;

    [Fact]
    public void Split_WhenTheTextFitsInOnePart_ShouldGiveItTrimmed()
    {
        Assert.Equal(["Hello."], TextParts.Split("  Hello.\n"));
    }

    [Fact]
    public void Split_WhenTheTextIsBlank_ShouldGiveNoPart()
    {
        Assert.Empty(TextParts.Split(" \n "));
    }

    [Fact]
    public void Split_WhenTheTextHasLines_ShouldCutAtLineEndsLosingNothing()
    {
        var lines = Enumerable.Range(1, 400).Select(i => $"Line {i}: something the person said, long enough to fill the parts.").ToList();

        var parts = TextParts.Split(string.Join("\n", lines));

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= Max));
        Assert.Equal(string.Join("\n", lines), string.Join("\n", parts));
    }

    [Fact]
    public void Split_WhenTheTextHasNoLineEnd_ShouldCutAtTheLimit()
    {
        var parts = TextParts.Split(new string('a', Max + 10));

        Assert.Equal([Max, 10], parts.Select(p => p.Length));
    }
}
