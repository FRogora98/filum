namespace Filum.Evals.LongMemEval;

internal sealed record LongMemEvalRunResult(IReadOnlyList<LongMemEvalResult> Results, decimal CostUsd, bool Stopped, int Planned, int SessionsUsed, string Description);
