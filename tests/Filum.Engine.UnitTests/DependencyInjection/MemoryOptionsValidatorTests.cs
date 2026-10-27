using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.DependencyInjection;

namespace Filum.Engine.UnitTests.DependencyInjection;

public sealed class MemoryOptionsValidatorTests
{
    private readonly MemoryOptionsValidator _validator = new();

    [Fact]
    public void Validate_WhenTheOptionsAreTheDefaults_ShouldSucceed() =>
        Assert.True(_validator.Validate(null, new MemoryOptions()).Succeeded);

    [Fact]
    public void Validate_WhenALimitIsNotAboveZero_ShouldFailNamingIt()
    {
        var result = _validator.Validate(null, new MemoryOptions { MaxToolCallsPerTurn = 0, EventsSearchMax = -1 });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Memory:MaxToolCallsPerTurn"));
        Assert.Contains(result.Failures!, f => f.Contains("Memory:EventsSearchMax"));
    }

    [Fact]
    public void Validate_WhenAnExcludedToolIsNotATool_ShouldFailNamingIt()
    {
        var result = _validator.Validate(null, new MemoryOptions { ExcludedTools = [ToolNames.MemoryDelete, "memory_delet"] });

        Assert.Equal("Memory:ExcludedTools names 'memory_delet', which is not a tool of the engine.", Assert.Single(result.Failures!));
    }
}
