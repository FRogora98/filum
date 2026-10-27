using Microsoft.Extensions.AI;

namespace Filum.Evals.Judging;

internal sealed class ModelClaimJudge(IChatClient model, ModelPrice price) : IClaimJudge, IPromptJudge
{
    private const string Yes = "yes";

    // A benchmark's own prompt, as it is; read as LongMemEval reads it: "yes" anywhere in the reply.
    public async Task<(bool Yes, decimal CostUsd)> YesAsync(string prompt, CancellationToken cancellationToken)
    {
        var (reply, cost) = await CallAsync(prompt, cancellationToken);
        return (reply.Contains(Yes, StringComparison.OrdinalIgnoreCase), cost);
    }

    public async Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken)
    {
        var (yes, cost) = await YesNoAsync($"{JudgePrompts.Claim}\n<<<\n{answer}\n>>>", cancellationToken);
        return new ClaimVerdict(yes, cost);
    }

    public async Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken)
    {
        var (yes, cost) = await YesNoAsync($"{JudgePrompts.Question}\n{question}\n\nReply:\n<<<\n{answer}\n>>>", cancellationToken);
        return new JudgeAnswer(yes, cost);
    }

    private async Task<(bool Yes, decimal Cost)> YesNoAsync(string prompt, CancellationToken cancellationToken)
    {
        var (reply, cost) = await CallAsync(prompt, cancellationToken);
        return (reply.Trim().StartsWith(Yes, StringComparison.OrdinalIgnoreCase), cost);
    }

    private async Task<(string Reply, decimal Cost)> CallAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await model.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
        return (response.Text, price.CostUsd(response.Usage));
    }
}
