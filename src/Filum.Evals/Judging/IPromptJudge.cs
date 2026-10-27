namespace Filum.Evals.Judging;

/// <summary>Asks a fixed model a yes/no prompt as it is, and reads the answer as LongMemEval does ("yes" anywhere).</summary>
internal interface IPromptJudge
{
    Task<(bool Yes, decimal CostUsd)> YesAsync(string prompt, CancellationToken cancellationToken);
}
