using Filum.Mcp.Tools;
using Microsoft.Extensions.AI;

namespace Filum.Mcp.UnitTests.Tools;

public sealed class PlainTextFunctionTests
{
    [Fact]
    public async Task InvokeAsync_WhenTheFunctionAnswersText_ShouldSendItAsPlainText()
    {
        var function = new PlainTextFunction(AIFunctionFactory.Create(() => "Done.", "done"));

        Assert.Equal("Done.", await function.InvokeAsync(new AIFunctionArguments()));
    }

    [Fact]
    public async Task InvokeAsync_WhenWrapped_ShouldKeepTheNameAndDescription()
    {
        var inner = AIFunctionFactory.Create(() => "Done.", "done", "Says it is done.");

        var function = new PlainTextFunction(inner);

        Assert.Equal(("done", "Says it is done."), (function.Name, function.Description));
        Assert.Equal(inner.JsonSchema.GetRawText(), function.JsonSchema.GetRawText());
        Assert.NotNull(await function.InvokeAsync(new AIFunctionArguments()));
    }
}
