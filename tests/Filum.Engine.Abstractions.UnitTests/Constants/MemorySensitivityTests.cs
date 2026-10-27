using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.UnitTests.Constants;

public sealed class MemorySensitivityTests
{
    [Fact]
    public void All_WhenRead_ShouldListTheLevelsFromTheLeastToTheMostProtected()
    {
        // The levels are written in files and events: their values are part of the formats.
        Assert.Equal(["normal", "sensitive", "private"], MemorySensitivity.All);
    }
}
