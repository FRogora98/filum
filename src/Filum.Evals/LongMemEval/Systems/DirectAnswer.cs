using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace Filum.Evals.LongMemEval.Systems;

// A failure of the call is the answer's error, so the run goes on.
internal static class DirectAnswer
{
    public static async Task<LongMemEvalAnswer> AskAsync(DirectModel model, string prompt, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var response = await model.Client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
            return new LongMemEvalAnswer(response.Text.Trim(), (int)(response.Usage?.InputTokenCount ?? 0), (int)(response.Usage?.OutputTokenCount ?? 0), model.Cost(response.Usage), clock.Elapsed.TotalSeconds, 1);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return LongMemEvalAnswer.Failed($"{exception.GetType().Name}: {exception.Message}", clock.Elapsed.TotalSeconds, 1);
        }
    }
}
