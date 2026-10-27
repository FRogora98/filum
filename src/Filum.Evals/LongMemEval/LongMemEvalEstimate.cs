using Filum.Evals.Judging;
using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.LongMemEval;

// From the history's size and each model's prices, before the run starts.
internal static class LongMemEvalEstimate
{
    // The platform layer, the core, the index and the tools a Filum turn sends besides the message.
    public const int FilumOverheadTokens = 7_000;

    // The answer and a tool call or two.
    public const double CallsPerFilumTurn = 2.5;

    public const int OutputTokensPerTurn = 300;

    public const int PromptTokens = 300;

    public const int CharactersPerToken = 4;
    public const int JudgeInputTokens = 500;
    public const int JudgeOutputTokens = 5;

    // A model without a price is not counted; the judge is counted for every answer.
    public static decimal Of(IReadOnlyList<LongMemEvalInstance> instances, IReadOnlyList<ILongMemEvalSystemStrategy> systems, IReadOnlyDictionary<string, ModelPrice> prices)
    {
        var total = 0m;
        foreach (var system in systems.Where(s => prices.ContainsKey(s.Model)))
        {
            foreach (var instance in instances)
            {
                var (input, output) = system.EstimatedTokens(instance);
                total += prices[system.Model].CostUsd(input, output);
            }
        }

        return total + instances.Count * systems.Count * JudgeModel.Price.CostUsd(JudgeInputTokens, JudgeOutputTokens);
    }

    // A Filum turn per part of every session and one for the question, each with the platform's overhead.
    public static (int Input, int Output) FilumTokens(LongMemEvalInstance instance)
    {
        var parts = instance.Sessions.SelectMany(s => LongMemEvalProtocol.SessionMessages(s.Date, s.Turns)).ToList();
        var input = parts.Sum(p => CallsPerFilumTurn * (FilumOverheadTokens + p.Length / (double)CharactersPerToken)) + CallsPerFilumTurn * FilumOverheadTokens;
        return ((int)input, (parts.Count + 1) * OutputTokensPerTurn);
    }
}
