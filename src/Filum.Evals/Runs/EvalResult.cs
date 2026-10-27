namespace Filum.Evals.Runs;

internal sealed record EvalResult(IReadOnlyList<ScenarioRun> Runs, decimal CostUsd, bool StoppedAtCap, int PlannedRuns, string? Target = null);
