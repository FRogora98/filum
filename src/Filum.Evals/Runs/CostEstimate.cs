using Filum.Evals.Hosts.Wire;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Runs;

// A turn is about 8,000 tokens in and 400 out, the judge about 600 in.
internal static class CostEstimate
{
    public const int InputTokensPerTurn = 8_000;
    public const int OutputTokensPerTurn = 400;
    public const int JudgeInputTokensPerTurn = 600;
    public const int JudgeOutputTokensPerTurn = 20;

    public static decimal Of(IReadOnlyList<Scenario> scenarios, IReadOnlyList<ModelDto> models, int repetitions, ModelPrice judge)
    {
        var turns = scenarios.Sum(s => s.TurnCount) * repetitions;
        var perModel = models.Sum(m => new ModelPrice(m.InputPricePerMillionUsd, m.OutputPricePerMillionUsd).CostUsd((long)turns * InputTokensPerTurn, (long)turns * OutputTokensPerTurn));
        var judged = (long)models.Count * turns;
        return perModel + judge.CostUsd(judged * JudgeInputTokensPerTurn, judged * JudgeOutputTokensPerTurn);
    }
}
