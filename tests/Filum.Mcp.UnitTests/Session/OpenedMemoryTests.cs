using Filum.Engine.Storage.InMemory;
using Filum.Mcp.Session;

namespace Filum.Mcp.UnitTests.Session;

public sealed class OpenedMemoryTests
{
    [Fact]
    public void Store_WhenNotOpenYet_ShouldThrow()
    {
        using var memory = new OpenedMemory();

        Assert.Throws<InvalidOperationException>(() => memory.Store);
    }

    [Fact]
    public void Open_WhenCalledTwice_ShouldRefuseTheSecond()
    {
        using var memory = new OpenedMemory();
        var owner = Guid.NewGuid();
        memory.Open(new InMemoryMemoryStore(), owner, pack: null);

        Assert.Throws<InvalidOperationException>(() => memory.Open(new InMemoryMemoryStore(), Guid.NewGuid(), pack: null));
        Assert.Equal(owner, memory.Owner);
    }
}
