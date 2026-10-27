using Filum.Mcp.Startup;

namespace Filum.Mcp.UnitTests.Startup;

// Spec 055: a replay sets today explicitly; it is never inferred from the memory.
public sealed class McpClockTests
{
    [Fact]
    public void Create_WhenADayIsGiven_ShouldBeThatDay()
    {
        var time = McpClock.Create("2026-05-11", TextWriter.Null);

        Assert.Equal(new DateOnly(2026, 5, 11), DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public void Create_WhenNothingOrSomethingInvalidIsGiven_ShouldBeTheSystemClockAndSayWhyForTheInvalid()
    {
        var errors = new StringWriter();

        Assert.Same(TimeProvider.System, McpClock.Create(null, errors));
        Assert.Same(TimeProvider.System, McpClock.Create("May 11", errors));
        Assert.Contains("FILUM_TODAY must be a day", errors.ToString());
    }
}
