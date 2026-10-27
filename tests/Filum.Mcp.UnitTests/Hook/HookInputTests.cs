using Filum.Mcp.Configuration;
using Filum.Mcp.Hook;

namespace Filum.Mcp.UnitTests.Hook;

public sealed class HookInputTests
{
    [Fact]
    public void Read_WhenTheInputIsAHooksJson_ShouldGiveThePromptAndTheSession()
    {
        var (text, session) = HookInput.Read("""{"session_id":"s-1","hook_event_name":"UserPromptSubmit","prompt":"I moved.","cwd":"/tmp"}""");

        Assert.Equal(("I moved.", "s-1"), (text, session));
    }

    [Fact]
    public void Read_WhenTheJsonHasNoSession_ShouldGiveThePromptAlone()
    {
        Assert.Equal(("Hello.", (string?)null), HookInput.Read("""{"prompt":"Hello."}"""));
    }

    [Theory]
    [InlineData("I moved to the coast.")]
    [InlineData("""{"message":"no prompt here"}""")]
    [InlineData("""{"prompt":42}""")]
    [InlineData("""["prompt"]""")]
    [InlineData("{ not json")]
    public void Read_WhenTheInputIsNotAHooksPrompt_ShouldGiveTheWholeInput(string input)
    {
        Assert.Equal((input, (string?)null), HookInput.Read(input));
    }

    [Fact]
    public void Home_WhenGivenAfterTheOption_ShouldBeThatFolder()
    {
        Assert.Equal("/memory", HookInput.Home(["log", "--home", "/memory"]));
    }

    [Fact]
    public void Home_WhenTheOptionHasNoValue_ShouldBeTheDefaultFolder()
    {
        Assert.Equal(HookInput.Home(["log"]), HookInput.Home(["log", "--home"]));
    }

    [Fact]
    public void Home_WhenNoFolderIsGiven_ShouldBeTheServersOwnDefault()
    {
        Assert.Equal(McpOptions.FromEnvironment().Home, HookInput.Home(["log"]));
    }

    [Fact]
    public void Conversation_WhenTwoPromptsShareASession_ShouldBeTheSame()
    {
        Assert.Equal(HookInput.Conversation("s-1"), HookInput.Conversation("s-1"));
        Assert.NotEqual(HookInput.Conversation("s-1"), HookInput.Conversation("s-2"));
    }

    [Fact]
    public void Conversation_WhenThereIsNoSession_ShouldBeANewOneEachTime()
    {
        Assert.NotEqual(HookInput.Conversation(null), HookInput.Conversation(null));
    }
}
