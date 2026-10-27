namespace Filum.Evals.Runs;

internal sealed record ModelScore(
    string Model,
    int Runs,
    int Succeeded,
    int ClaimedNotDone,
    int RulesKept,
    int RulesChecked,
    int Turns,
    int FailedTurns,
    double AverageInputTokens,
    double AverageOutputTokens,
    decimal CostPerTurnUsd,
    decimal? CostPerSucceededUsd,
    double MedianLatencySeconds,
    int Answered = 0,
    int Refused = 0)
{
    public static IReadOnlyList<ModelScore> Of(EvalResult result) =>
        result.Runs.GroupBy(r => r.Model).Select(g => Of(g.Key, g.ToList())).ToList();

    private static ModelScore Of(string model, IReadOnlyList<ScenarioRun> runs)
    {
        var turns = runs.SelectMany(r => r.Turns).ToList();
        var rules = runs.SelectMany(r => r.Checks).Where(c => c.Result.Expectation.Rule).ToList();
        var cost = runs.Sum(r => r.CostUsd);
        var succeeded = runs.Count(r => r.Succeeded);
        var latencies = turns.Select(t => t.LatencySeconds).Order().ToList();
        return new ModelScore(
            model,
            runs.Count,
            succeeded,
            runs.Sum(r => r.ClaimedNotDone.Count),
            rules.Count(c => c.Result.Passed),
            rules.Count,
            turns.Count,
            turns.Count(t => t.Error is not null),
            turns.Count == 0 ? 0 : turns.Average(t => t.InputTokens),
            turns.Count == 0 ? 0 : turns.Average(t => t.OutputTokens),
            turns.Count == 0 ? 0 : cost / turns.Count,
            succeeded == 0 ? null : cost / succeeded,
            latencies.Count == 0 ? 0 : latencies[latencies.Count / 2],
            turns.Count(t => t.Error is null),
            turns.Count(t => t.Refused));
    }
}
