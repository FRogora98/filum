using Filum.Evals.Checks;

namespace Filum.Evals.Runs;

internal sealed record TurnAssessment(IReadOnlyList<CheckedExpectation> Checks, bool ClaimedNotDone, decimal JudgeCostUsd);
