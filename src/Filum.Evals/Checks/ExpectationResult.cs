using Filum.Evals.Scenarios;

namespace Filum.Evals.Checks;

internal sealed record ExpectationResult(Expectation Expectation, bool Passed, string Detail);
