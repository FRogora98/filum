using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Filum.Evals.Runs;

internal static class EvalReport
{
    private const int AnswerExcerptLength = 140;

    public static string Markdown(EvalResult result, TimeProvider time)
    {
        var text = new StringBuilder();
        AppendHeader(text, result, time);
        AppendScores(text, result);
        text.AppendLine();
        text.AppendLine("## Failures");
        text.AppendLine();
        var failures = result.Runs.SelectMany(Failures).ToList();
        text.AppendLine(failures.Count == 0 ? "None." : string.Join('\n', failures));
        return text.ToString();
    }

    public static string Json(EvalResult result) =>
        JsonSerializer.Serialize(new { result.CostUsd, result.StoppedAtCap, result.PlannedRuns, Scores = ModelScore.Of(result), result.Runs }, JsonDefaults.Indented);

    public static Task<string> WriteAsync(EvalResult result, string outDir, TimeProvider time, CancellationToken cancellationToken) =>
        ResultsFolder.WriteAsync(
            Path.Combine(outDir, time.GetLocalNow().ToString(ResultsFolder.TimeFormat, CultureInfo.InvariantCulture)),
            [(ResultsFolder.ReportFile, Markdown(result, time)), (ResultsFolder.ResultsFile, Json(result))],
            cancellationToken);

    private static void AppendHeader(StringBuilder text, EvalResult result, TimeProvider time)
    {
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"# Eval — {time.GetLocalNow():yyyy-MM-dd HH:mm}");
        text.AppendLine();
        if (result.Target is not null)
        {
            text.AppendLine(culture, $"Host: {result.Target}");
            text.AppendLine();
        }

        text.AppendLine(culture, $"{result.Runs.Count} of {result.PlannedRuns} runs · total cost ${result.CostUsd:0.0000} (judge included){(result.StoppedAtCap ? " · **stopped at the cap**" : "")}");
        text.AppendLine();
    }

    private static void AppendScores(StringBuilder text, EvalResult result)
    {
        text.AppendLine("| Model | Scenarios succeeded | Claimed but not done | Rules kept | Turns (failed) | Avg tokens in / out | Cost per turn | Cost per success | Median latency | Answered / refused |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var s in ModelScore.Of(result).OrderByDescending(s => (double)s.Succeeded / Math.Max(1, s.Runs)).ThenBy(s => s.CostPerTurnUsd))
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {s.Model} | {s.Succeeded}/{s.Runs} ({Percent(s.Succeeded, s.Runs)}) | {s.ClaimedNotDone} | {s.RulesKept}/{s.RulesChecked} | {s.Turns} ({s.FailedTurns}) | {s.AverageInputTokens:0} / {s.AverageOutputTokens:0} | ${s.CostPerTurnUsd:0.00000} | {(s.CostPerSucceededUsd is { } c ? $"${c:0.0000}" : "—")} | {s.MedianLatencySeconds:0.0} s | {s.Answered} / {s.Refused} |");
        }
    }

    private static IEnumerable<string> Failures(ScenarioRun r) =>
        r.Checks.Where(c => !c.Result.Passed).Select(c => $"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition} · turn {c.Turn}: expected {c.Result.Expectation} — {c.Result.Detail}")
            .Concat(r.SetupError is null ? [] : [$"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition}: {r.SetupError}"])
            .Concat(r.ClaimedNotDone.Select(t => $"- **{r.Model}** · {r.ScenarioId} · #{r.Repetition} · turn {t}: **claimed a change that was not made** — {Excerpt.Quoted(r.Turns[t - 1].Answer, AnswerExcerptLength)}"));

    private static string Percent(int part, int whole) => whole == 0 ? "—" : $"{100.0 * part / whole:0}%";
}
