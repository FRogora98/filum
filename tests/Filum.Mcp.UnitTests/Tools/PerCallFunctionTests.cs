using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Tools;
using Filum.Mcp.Tools;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace Filum.Mcp.UnitTests.Tools;

public sealed class PerCallFunctionTests : IDisposable
{
    private readonly FilumEngine _engine = FilumEngine.Create(new InMemoryMemoryStore());
    private readonly Guid _person = Guid.NewGuid();
    private int _made;

    public void Dispose() => _engine.Dispose();

    [Fact]
    public async Task InvokeAsync_WhenTheToolAnswers_ShouldGiveItsTextOnFreshToolsEachCall()
    {
        var write = Function("memory_write");

        var first = await write.InvokeAsync(Arguments("/notes/a.md"));
        await write.InvokeAsync(Arguments("/notes/b.md"));

        Assert.False(Assert.IsType<CallToolResult>(first).IsError);
        Assert.Equal(2, _made);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheToolRefuses_ShouldGiveAToolErrorWithTheReason()
    {
        var write = Function("memory_write");

        var refused = Assert.IsType<CallToolResult>(await write.InvokeAsync(Arguments("/notes/../secret.md")));

        Assert.True(refused.IsError);
        Assert.Contains("'..'", Assert.IsType<TextContentBlock>(Assert.Single(refused.Content)).Text);
    }

    private PerCallFunction Function(string name) =>
        new(_engine.Tools.Catalog().Single(t => t.Name == name), Fresh);

    private MemoryTools Fresh()
    {
        _made++;
        return _engine.Tools.Create(_person, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
    }

    private static AIFunctionArguments Arguments(string path) => new() { ["path"] = path, ["content"] = "x\n" };
}
