using System.Text.Json;
using Filum.Mcp.Tools;

namespace Filum.Mcp.UnitTests.Tools;

public sealed class ToolTextTests
{
    [Fact]
    public void Unwrap_WhenTheResultIsAJsonString_ShouldGiveItsText()
    {
        Assert.Equal("Done.", ToolText.Unwrap(JsonSerializer.SerializeToElement("Done.")));
    }

    [Fact]
    public void Unwrap_WhenTheResultIsText_ShouldGiveIt()
    {
        Assert.Equal("Done.", ToolText.Unwrap("Done."));
    }

    [Fact]
    public void Unwrap_WhenTheResultIsNotText_ShouldGiveNull()
    {
        Assert.Null(ToolText.Unwrap(JsonSerializer.SerializeToElement(new { done = true })));
        Assert.Null(ToolText.Unwrap(42));
        Assert.Null(ToolText.Unwrap(null));
    }

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly10!", 10, "exactly10!")]
    [InlineData("a little too long", 8, "a little…")]
    public void Cut_WhenTheTextIsLongerThanTheLength_ShouldEndWithAnEllipsis(string text, int length, string expected)
    {
        Assert.Equal(expected, ToolText.Cut(text, length));
    }
}
