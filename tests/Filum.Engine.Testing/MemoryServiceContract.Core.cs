using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Instructions;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task EnsureCoreAsync_WhenCalledTwice_ShouldCreateTheCoreOnceFromTheTemplate()
    {
        var first = await _memory.EnsureCoreAsync(_user, None);
        var second = await _memory.EnsureCoreAsync(_user, None);

        Assert.Equal(PlatformInstructions.CoreTemplate, first);
        Assert.Equal(first, second);
        var history = Ok(await _memory.GetHistoryAsync(_user, "/filum.md", None));
        var created = Assert.Single(history);
        Assert.Equal((MemoryOperation.CreateCore, MemoryAuthor.Platform), (created.Operation, created.Author));
    }

    [Fact]
    public async Task UndoAsync_WhenTheChangeIsTheCoresCreation_ShouldRefuseAndKeepTheCore()
    {
        await _memory.EnsureCoreAsync(_user, None);
        var created = Assert.Single(Ok(await _memory.GetHistoryAsync(_user, "/filum.md", None)));

        Assert.Contains("cannot be deleted", Refused(await _memory.UndoAsync(_user, MemoryActor.Person, created.Id, None)));
        Assert.Equal(PlatformInstructions.CoreTemplate, await _memory.EnsureCoreAsync(_user, None));
        Assert.Single(Ok(await _memory.GetHistoryAsync(_user, "/filum.md", None)));
    }
}
