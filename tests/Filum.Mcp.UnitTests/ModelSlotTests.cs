using Filum.Mcp.UnitTests.Fakes;

namespace Filum.Mcp.UnitTests;

public sealed class ModelSlotTests
{
    [Fact]
    public void Current_WhenNothingWasLoaded_ShouldBeNull()
    {
        using var slot = new ModelSlot();

        Assert.Null(slot.Current);
    }

    [Fact]
    public void TryFill_WhenAModelIsAlreadyThere_ShouldRefuseTheSecondAndKeepTheFirst()
    {
        using var slot = new ModelSlot();
        var first = new WordEmbedder();

        Assert.True(slot.TryFill(first));
        Assert.False(slot.TryFill(new WordEmbedder()));
        Assert.Same(first, slot.Current);
    }
}
