namespace Filum.Evals.LongMemEval;

internal sealed record LongMemEvalScore(
    string System, string Model, int Questions, int Repetitions, double Accuracy, double AccuracySpread, IReadOnlyDictionary<string, double> ByAbility,
    int Errors, double InputTokensPerQuestion, double OutputTokensPerQuestion, decimal CostPerQuestionUsd, decimal? CostPerCorrectUsd, double MedianSeconds, double TurnsPerQuestion)
{
    public static IReadOnlyList<LongMemEvalScore> Of(LongMemEvalRunResult run) =>
        run.Results.GroupBy(r => (r.System, r.Model)).Select(g =>
        {
            var perRepetition = g.GroupBy(r => r.Repetition).Select(rep => rep.Average(r => r.Correct ? 1.0 : 0.0)).ToList();
            var mean = perRepetition.Average();
            var spread = perRepetition.Count < 2 ? 0 : Math.Sqrt(perRepetition.Sum(a => (a - mean) * (a - mean)) / (perRepetition.Count - 1));
            var correct = g.Count(r => r.Correct);
            var cost = g.Sum(r => r.CostUsd);
            var seconds = g.Select(r => r.Seconds).Order().ToList();
            return new LongMemEvalScore(
                g.Key.System, g.Key.Model, g.Select(r => r.QuestionId).Distinct().Count(), perRepetition.Count, mean, spread,
                LongMemEvalAbilities.All.Where(a => g.Any(r => r.Ability == a)).ToDictionary(a => a, a => g.Where(r => r.Ability == a).Average(r => r.Correct ? 1.0 : 0.0)),
                g.Count(r => r.Error is not null), g.Average(r => r.InputTokens), g.Average(r => r.OutputTokens),
                cost / g.Count(), correct == 0 ? null : cost / correct, seconds[seconds.Count / 2], g.Average(r => r.Turns));
        }).ToList();
}
