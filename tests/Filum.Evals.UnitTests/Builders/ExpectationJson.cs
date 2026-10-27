using Filum.Evals.Scenarios;

namespace Filum.Evals.UnitTests.Builders;

internal static class ExpectationJson
{
    public static Expectation Parse(string json) =>
        ScenarioLoader.Parse($$"""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[{{json}}]}]}""", "test").Turns[0].Expect[0];
}
