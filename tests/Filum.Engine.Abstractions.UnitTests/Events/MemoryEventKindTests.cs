using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Abstractions.UnitTests.Events;

public sealed class MemoryEventKindTests
{
    [Fact]
    public void Spoken_WhenRead_ShouldBeWhatWasSaidAnsweredOrBroughtIn()
    {
        Assert.Equal([MemoryEventKind.Said, MemoryEventKind.Answered, MemoryEventKind.Imported], MemoryEventKind.Spoken);
    }
}
