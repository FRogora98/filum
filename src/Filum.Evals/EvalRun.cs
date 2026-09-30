using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Filum.Evals;

/// <summary>A cost estimate before a run: a turn is about 8,000 tokens in and 400 out, the judge about 600 in.</summary>
public static class CostEstimate
{
    public const int InputTokensPerTurn = 8_000;
    public const int OutputTokensPerTurn = 400;
    public const int JudgeInputTokensPerTurn = 600;
    public const int JudgeOutputTokensPerTurn = 20;

    public static decimal Of(IReadOnlyList<Scenario> scenarios, IReadOnlyList<ModelDto> models, int repetitions, decimal judgeInputPrice, decimal judgeOutputPrice)
    {
        var turns = scenarios.Sum(s => s.TurnCount) * repetitions;
        var perModel = models.Sum(m => turns * (InputTokensPerTurn * m.InputPricePerMillionUsd + OutputTokensPerTurn * m.OutputPricePerMillionUsd) / 1_000_000m);
        var judge = models.Count * turns * (JudgeInputTokensPerTurn * judgeInputPrice + JudgeOutputTokensPerTurn * judgeOutputPrice) / 1_000_000m;
        return perModel + judge;
    }
}

/// <param name="Target">Where the scenarios were played, for the report's first line.</param>
public sealed record EvalResult(IReadOnlyList<ScenarioRun> Runs, decimal CostUsd, bool StoppedAtCap, int PlannedRuns, string? Target = null);

/// <summary>Every scenario, for every model, as many times as asked, until done or until the spending cap is reached.</summary>
/// <summary>Plays one scenario once; a runner with a budget of its own (agent sessions) says when it cannot start another.</summary>
public interface IScenarioRunner
{
    Task<ScenarioRun> RunAsync(Scenario scenario, string model, int repetition, CancellationToken cancellationToken);

    bool CanStart(Scenario scenario) => true;
}

public sealed class EvalRun(IScenarioRunner runner, decimal capUsd, TextWriter? progress = null, string? target = null)
{
    public async Task<EvalResult> RunAsync(IReadOnlyList<Scenario> scenarios, IReadOnlyList<string> models, int repetitions, CancellationToken cancellationToken)
    {
        var runs = new List<ScenarioRun>();
        var spent = 0m;
        var planned = scenarios.Count * models.Count * repetitions;
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            foreach (var model in models)
            {
                foreach (var scenario in scenarios)
                {
                    if (spent >= capUsd || !runner.CanStart(scenario))
                    {
                        progress?.WriteLine(spent >= capUsd
                            ? $"Stopped: the cap of ${capUsd} is reached after {runs.Count} of {planned} runs."
                            : $"Stopped: no sessions left for the next scenario after {runs.Count} of {planned} runs.");
                        return new EvalResult(runs, spent, StoppedAtCap: true, planned, target);
                    }

                    var run = await runner.RunAsync(scenario, model, repetition, cancellationToken);
                    runs.Add(run);
                    spent += run.CostUsd + run.JudgeCostUsd;
                    progress?.WriteLine($"[{runs.Count}/{planned}] {model} · {scenario.Id} · #{repetition}: {(run.Succeeded ? "ok" : "failed")}{(run.ClaimedNotDone.Count > 0 ? " · claimed but not done" : "")} · ${spent:0.0000}");
                }
            }
        }

        return new EvalResult(runs, spent, StoppedAtCap: false, planned, target);
    }
}

public sealed record ModelScore(
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
    int Refused = 0);

/// <summary>The comparison: one row per model, then every failed expectation with the turn that failed it.</summary>
public static class EvalReport
{
    public static IReadOnlyList<ModelScore> Score(EvalResult result) =>
        result.Runs.GroupBy(r => r.Model).Select(g =>
        {
            var turns = g.SelectMany(r => r.Turns).ToList();
            var rules = g.SelectMany(r => r.Checks).Where(c => c.Result.Expectation.Rule).ToList();
            var cost = g.Sum(r => r.CostUsd);
            var succeeded = g.Count(r => r.Succeeded);
            var latencies = turns.Select(t => t.LatencySeconds).Order().ToList();
            return new ModelScore(
                g.Key,
                g.Count(),
                succeeded,
                g.Sum(r => r.ClaimedNotDone.Count),
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
        }).ToList();

    public static string Markdown(EvalResult result)
    {
        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"# Eval — {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        if (result.Target is not null)
        {
            text.AppendLine(culture, $"Host: {result.Target}");
            text.AppendLine();
        }

        text.AppendLine(culture, $"{result.Runs.Count} of {result.PlannedRuns} runs · total cost ${result.CostUsd:0.0000} (judge included){(result.StoppedAtCap ? " · **stopped at the cap**" : "")}");
        text.AppendLine();
        text.AppendLine("| Model | Scenarios succeeded | Claimed but not done | Rules kept | Turns (failed) | Avg tokens in / out | Cost per turn | Cost per success | Median latency | Answered / refused |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var s in Score(result).OrderByDescending(s => (double)s.Succeeded / Math.Max(1, s.Runs)).ThenBy(s => s.CostPerTurnUsd))
        {
            text.AppendLine(culture,
                $"| {s.Model} | {s.Succeeded}/{s.Runs} ({Percent(s.Succeeded, s.Runs)}) | {s.ClaimedNotDone} | {s.RulesKept}/{s.RulesChecked} | {s.Turns} ({s.FailedTurns}) | {s.AverageInputTokens:0} / {s.AverageOutputTokens:0} | ${s.CostPerTurnUsd:0.00000} | {(s.CostPerSucceededUsd is { } c ? $"${c:0.0000}" : "—")} | {s.MedianLatencySeconds:0.0} s | {s.Answered} / {s.Refused} |");
        }

        text.AppendLine();
        text.AppendLine("## Failures");
        text.AppendLine();
        var failures = result.Runs.SelectMany(r =>
            r.Checks.Where(c => !c.Result.Passed).Select(c => $"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition} · turn {c.Turn}: expected {c.Result.Expectation} — {c.Result.Detail}")
                .Concat(r.SetupError is null ? [] : [$"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition}: {r.SetupError}"])
                .Concat(r.ClaimedNotDone.Select(t => $"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition} · turn {t}: **claimed a change that was not made** — \"{Short(r.Turns[t - 1].Answer)}\"")))
            .ToList();
        text.AppendLine(failures.Count == 0 ? "None." : string.Join('\n', failures));
        return text.ToString();
    }

    /// <summary>Writes report.md and results.json into a new folder named after the time; returns the folder.</summary>
    public static async Task<string> WriteAsync(EvalResult result, string outDir)
    {
        var folder = Path.Combine(outDir, DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "report.md"), Markdown(result));
        await File.WriteAllTextAsync(Path.Combine(folder, "results.json"), Json(result));
        return folder;
    }

    public static string Json(EvalResult result) =>
        JsonSerializer.Serialize(new { result.CostUsd, result.StoppedAtCap, result.PlannedRuns, Scores = Score(result), result.Runs }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

    private static string Percent(int part, int whole) => whole == 0 ? "—" : $"{100.0 * part / whole:0}%";

    private static string Short(string text) => (text.Length <= 140 ? text : text[..137] + "…").ReplaceLineEndings(" ");
}
