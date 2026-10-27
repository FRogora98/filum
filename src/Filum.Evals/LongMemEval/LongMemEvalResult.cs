namespace Filum.Evals.LongMemEval;

internal sealed record LongMemEvalResult(
    string System, string Model, string QuestionId, string Ability, int Repetition, bool Correct, string Answer,
    int InputTokens, int OutputTokens, decimal CostUsd, decimal JudgeCostUsd, double Seconds, int Turns, string? Error, string? ModelVersion);
