using Filum.Evals.Checks;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Runs;

internal sealed record ScenarioRun(
    string ScenarioId,
    string Model,
    int Repetition,
    IReadOnlyList<TurnRecord> Turns,
    IReadOnlyList<CheckedExpectation> Checks,
    IReadOnlyList<int> ClaimedNotDone,
    decimal JudgeCostUsd,
    IReadOnlyList<MemorySnapshot>? Memories = null,
    string? SetupError = null)
{
    public bool Succeeded => SetupError is null && Checks.All(c => c.Result.Passed) && Turns.All(t => t.Error is null || t.Refused && ExpectsRefusal(t.Index));

    public decimal CostUsd => Turns.Sum(t => t.CostUsd);

    private bool ExpectsRefusal(int turn) => Checks.Any(c => c.Turn == turn && c.Result.Expectation.Type == ExpectationTypes.Refused);
}
