using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Filum.Evals.LongMemEval;

internal static class LongMemEvalReport
{
    private const int ErrorsShown = 20;
    private const int ErrorLength = 200;

    public static string Markdown(LongMemEvalRunResult run, TimeProvider time)
    {
        var text = new StringBuilder();
        AppendHeader(text, run, time);
        AppendScores(text, run);
        AppendVersions(text, run);
        AppendErrors(text, run);
        return text.ToString();
    }

    public static Task<string> WriteAsync(LongMemEvalRunResult run, string outDir, TimeProvider time, CancellationToken cancellationToken) =>
        ResultsFolder.WriteAsync(
            Path.Combine(outDir, time.GetLocalNow().ToString(ResultsFolder.TimeFormat, CultureInfo.InvariantCulture)),
            [
                (ResultsFolder.ReportFile, Markdown(run, time)),
                (ResultsFolder.ResultsFile, JsonSerializer.Serialize(new { run.Description, run.CostUsd, run.Stopped, run.Planned, run.SessionsUsed, Scores = LongMemEvalScore.Of(run), run.Results }, JsonDefaults.Indented))
            ],
            cancellationToken);

    private static void AppendHeader(StringBuilder text, LongMemEvalRunResult run, TimeProvider time)
    {
        var c = CultureInfo.InvariantCulture;
        text.AppendLine(c, $"# LongMemEval — {time.GetLocalNow():yyyy-MM-dd HH:mm}");
        text.AppendLine();
        if (run.Description.Length > 0)
        {
            text.AppendLine(run.Description);
            text.AppendLine();
        }

        text.AppendLine(c, $"{run.Results.Count} answers · model cost of this run ${run.CostUsd:0.0000}, of every answer ${run.Results.Sum(r => r.CostUsd + r.JudgeCostUsd):0.0000} (judge included) · {run.SessionsUsed} agent sessions{(run.Stopped ? " · **stopped at a cap**" : "")}");
        text.AppendLine();
    }

    private static void AppendScores(StringBuilder text, LongMemEvalRunResult run)
    {
        var c = CultureInfo.InvariantCulture;
        text.AppendLine(CultureInfo.InvariantCulture, $"| System | Model | Questions × reps | Accuracy (± spread) | {string.Join(" | ", LongMemEvalAbilities.All)} | Errors | Tokens in / out per question | $ per question | $ per correct | Median s |");
        text.AppendLine(CultureInfo.InvariantCulture, $"|---|---|---|---|{string.Concat(LongMemEvalAbilities.All.Select(_ => "---|"))}---|---|---|---|---|");
        foreach (var s in LongMemEvalScore.Of(run).OrderByDescending(s => s.Accuracy))
        {
            var abilities = string.Join(" | ", LongMemEvalAbilities.All.Select(a => s.ByAbility.TryGetValue(a, out var v) ? $"{v:P0}" : "—"));
            text.AppendLine(c,
                $"| {s.System} | {s.Model} | {s.Questions} × {s.Repetitions} | {s.Accuracy:P1} (± {s.AccuracySpread:P1}) | {abilities} | {s.Errors} | {s.InputTokensPerQuestion:0} / {s.OutputTokensPerQuestion:0} | ${s.CostPerQuestionUsd:0.0000} | {(s.CostPerCorrectUsd is { } pc ? $"${pc:0.0000}" : "—")} | {s.MedianSeconds:0.0} |");
        }
    }

    private static void AppendVersions(StringBuilder text, LongMemEvalRunResult run)
    {
        var versions = run.Results.Select(r => r.ModelVersion).OfType<string>().Distinct().ToList();
        if (versions.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"Agent model versions: {string.Join(", ", versions)}. Agent sessions run on a subscription: their cost is not in the dollars above.");
        }
    }

    private static void AppendErrors(StringBuilder text, LongMemEvalRunResult run)
    {
        var errors = run.Results.Where(r => r.Error is not null).Take(ErrorsShown).ToList();
        if (errors.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"## Errors (first {ErrorsShown})");
            text.AppendLine();
            foreach (var e in errors)
            {
                var error = e.Error ?? string.Empty;
                text.AppendLine(CultureInfo.InvariantCulture, $"- {e.System} · {e.Model} · {e.QuestionId} #{e.Repetition}: {error.ReplaceLineEndings(" ")[..Math.Min(ErrorLength, error.Length)]}");
            }
        }
    }
}
