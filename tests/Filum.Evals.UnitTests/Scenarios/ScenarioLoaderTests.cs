using Filum.Evals.Scenarios;
using Filum.Evals.UnitTests.Builders;

namespace Filum.Evals.UnitTests.Scenarios;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void Parse_WhenTheExpectationTypeIsUnknown_ShouldThrowAClearError()
    {
        var error = Assert.Throws<InvalidDataException>(() => ScenarioLoader.Parse("""{"id":"x","language":"en","turns":[{"chat":"a","message":"m","expect":[{"type":"magic"}]}]}""", "x.json"));

        Assert.Contains("unknown expectation type 'magic'", error.Message);
    }

    [Theory]
    [InlineData("""{"type":"judge","pass_if":"no"}""")]
    [InlineData("""{"type":"judge","question":"Is it?","pass_if":"maybe"}""")]
    [InlineData("""{"type":"host_unchanged"}""")]
    [InlineData("""{"type":"delete_everything"}""")]
    public void Parse_WhenAnExpectationIsMalformedOrUnknown_ShouldStopBeforeAnyTurnNamingTheFileAndTheTurn(string json)
    {
        var failure = Assert.Throws<InvalidDataException>(() => ExpectationJson.Parse(json));

        Assert.StartsWith("test: turn 1", failure.Message);
    }
}
