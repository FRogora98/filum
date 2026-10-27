namespace Filum.Evals.LongMemEval;

// Agent sessions are counted apart from tokens and dollars.
internal sealed record LongMemEvalAnswer(string Answer, int InputTokens, int OutputTokens, decimal CostUsd, double Seconds, int Turns, string? Error = null, string? ModelVersion = null)
{
    public static LongMemEvalAnswer Failed(string error, double seconds = 0, int turns = 0) => new(string.Empty, 0, 0, 0, seconds, turns, error);
}
