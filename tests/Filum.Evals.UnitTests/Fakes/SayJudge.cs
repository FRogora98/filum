using Filum.Evals.Judging;

namespace Filum.Evals.UnitTests.Fakes;

internal sealed class SayJudge : IPromptJudge
{
    public List<string> Prompts { get; } = [];

    public Task<(bool Yes, decimal CostUsd)> YesAsync(string prompt, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        return Task.FromResult((prompt.Contains("off-by-one"), 0.0001m));
    }
}
