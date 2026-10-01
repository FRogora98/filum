using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Filum.Evals.LongMemEval;

/// <summary>One question played once by one system, judged.</summary>
public sealed record LmeResult(
    string System, string Model, string QuestionId, string Ability, int Repetition, bool Correct, string Answer,
    int InputTokens, int OutputTokens, decimal CostUsd, decimal JudgeCostUsd, double Seconds, int Turns, string? Error, string? ModelVersion);

public sealed record LmeRunResult(IReadOnlyList<LmeResult> Results, decimal CostUsd, bool Stopped, int Planned, int SessionsUsed, string Description);

/// <summary>What a run is estimated to cost before it starts, from the history's size and each model's prices.</summary>
public static class LmeEstimate
{
    /// <summary>The platform layer, the core, the index and the tools a Filum turn sends besides the message.</summary>
    public const int FilumOverheadTokens = 7_000;

    /// <summary>Model calls per Filum turn: the answer and a tool call or two.</summary>
    public const double CallsPerFilumTurn = 2.5;

    public const int OutputTokensPerTurn = 300;
    public const int JudgeInputTokens = 500;

    public static (int Input, int Output) FilumTokens(LmeInstance instance)
    {
        var parts = instance.Sessions.SelectMany(s => LmeProtocol.SessionMessages(s.Date, s.Turns)).ToList();
        var input = parts.Sum(p => CallsPerFilumTurn * (FilumOverheadTokens + p.Length / 4.0)) + CallsPerFilumTurn * FilumOverheadTokens;
        return ((int)input, (parts.Count + 1) * OutputTokensPerTurn);
    }

    public static decimal Usd(int input, int output, decimal inputPrice, decimal outputPrice) => (input * inputPrice + output * outputPrice) / 1_000_000m;
}

/// <summary>
/// Every question, for every system, as many times as asked, judged with LongMemEval's prompts, until done or until
/// the dollar cap or the agent-session cap is reached. With a journal every answer is written the moment it is judged,
/// and answers already in it are not played again: a run that stops resumes where it stopped. The caps count only what
/// this run spends.
/// </summary>
public sealed class LmeRun(IPromptJudge judge, decimal capUsd, int maxSessions = int.MaxValue, TextWriter? progress = null, string description = "", string? journal = null)
{
    private static readonly JsonSerializerOptions JournalJson = new(JsonSerializerDefaults.Web);

    /// <summary>The answers a journal already holds, one JSON line each.</summary>
    public static IReadOnlyList<LmeResult> ReadJournal(string path) =>
        File.Exists(path)
            ? File.ReadLines(path).Where(l => l.Trim().Length > 0).Select(l => JsonSerializer.Deserialize<LmeResult>(l, JournalJson)!).ToList()
            : [];

    public async Task<LmeRunResult> RunAsync(IReadOnlyList<LmeInstance> instances, IReadOnlyList<ILmeSystem> systems, int repetitions, CancellationToken cancellationToken)
    {
        var results = journal is null ? new List<LmeResult>() : ReadJournal(journal).ToList();
        var done = results.Select(r => (r.System, r.Model, r.QuestionId, r.Repetition)).ToHashSet();
        var spent = 0m;
        var sessions = 0;
        var planned = instances.Count * systems.Count * repetitions;
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            foreach (var system in systems)
            {
                foreach (var instance in instances)
                {
                    if (done.Contains((system.Name, system.Model, instance.QuestionId, repetition)))
                    {
                        continue;
                    }

                    var needed = system.SessionsFor(instance);
                    if (spent >= capUsd || sessions + needed > maxSessions)
                    {
                        progress?.WriteLine(spent >= capUsd
                            ? $"Stopped: the cap of ${capUsd} is reached after {results.Count} of {planned}."
                            : $"Stopped: no agent sessions left after {results.Count} of {planned}.");
                        return new LmeRunResult(results, spent, true, planned, sessions, description);
                    }

                    sessions += needed;
                    var answer = await system.AnswerAsync(instance, cancellationToken);
                    var (correct, judgeCost) = answer.Error is null && answer.Answer.Length > 0
                        ? await judge.YesAsync(LmeJudge.Prompt(instance, answer.Answer), cancellationToken)
                        : (false, 0m);
                    var result = new LmeResult(system.Name, system.Model, instance.QuestionId, instance.Ability, repetition, correct, answer.Answer,
                        answer.InputTokens, answer.OutputTokens, answer.CostUsd, judgeCost, answer.Seconds, answer.Turns, answer.Error, answer.ModelVersion);
                    results.Add(result);
                    if (journal is not null)
                    {
                        await File.AppendAllTextAsync(journal, JsonSerializer.Serialize(result, JournalJson) + "\n", cancellationToken);
                    }

                    spent += answer.CostUsd + judgeCost;
                    progress?.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"[{results.Count}/{planned}] {system.Name} · {system.Model} · {instance.QuestionId} ({instance.Ability}) #{repetition}: {(answer.Error is not null ? "error" : correct ? "correct" : "wrong")} · ${spent:0.0000}"));
                }
            }
        }

        return new LmeRunResult(results, spent, false, planned, sessions, description);
    }
}

public sealed record LmeScore(
    string System, string Model, int Questions, int Repetitions, double Accuracy, double AccuracySpread, IReadOnlyDictionary<string, double> ByAbility,
    int Errors, double InputTokensPerQuestion, double OutputTokensPerQuestion, decimal CostPerQuestionUsd, decimal? CostPerCorrectUsd, double MedianSeconds, double TurnsPerQuestion);

/// <summary>The comparison: one row per system and model, accuracy over repetitions, per ability, and what it cost.</summary>
public static class LmeReport
{
    public static IReadOnlyList<LmeScore> Score(LmeRunResult run) =>
        run.Results.GroupBy(r => (r.System, r.Model)).Select(g =>
        {
            var perRepetition = g.GroupBy(r => r.Repetition).Select(rep => rep.Average(r => r.Correct ? 1.0 : 0.0)).ToList();
            var mean = perRepetition.Average();
            var spread = perRepetition.Count < 2 ? 0 : Math.Sqrt(perRepetition.Sum(a => (a - mean) * (a - mean)) / (perRepetition.Count - 1));
            var correct = g.Count(r => r.Correct);
            var cost = g.Sum(r => r.CostUsd);
            var seconds = g.Select(r => r.Seconds).Order().ToList();
            return new LmeScore(
                g.Key.System, g.Key.Model, g.Select(r => r.QuestionId).Distinct().Count(), perRepetition.Count, mean, spread,
                LmeAbilities.All.Where(a => g.Any(r => r.Ability == a)).ToDictionary(a => a, a => g.Where(r => r.Ability == a).Average(r => r.Correct ? 1.0 : 0.0)),
                g.Count(r => r.Error is not null), g.Average(r => r.InputTokens), g.Average(r => r.OutputTokens),
                cost / g.Count(), correct == 0 ? null : cost / correct, seconds[seconds.Count / 2], g.Average(r => r.Turns));
        }).ToList();

    public static string Markdown(LmeRunResult run)
    {
        var text = new StringBuilder();
        var c = CultureInfo.InvariantCulture;
        text.AppendLine(c, $"# LongMemEval — {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        if (run.Description.Length > 0)
        {
            text.AppendLine(run.Description);
            text.AppendLine();
        }

        text.AppendLine(c, $"{run.Results.Count} answers · model cost of this run ${run.CostUsd:0.0000}, of every answer ${run.Results.Sum(r => r.CostUsd + r.JudgeCostUsd):0.0000} (judge included) · {run.SessionsUsed} agent sessions{(run.Stopped ? " · **stopped at a cap**" : "")}");
        text.AppendLine();
        text.AppendLine($"| System | Model | Questions × reps | Accuracy (± spread) | {string.Join(" | ", LmeAbilities.All)} | Errors | Tokens in / out per question | $ per question | $ per correct | Median s |");
        text.AppendLine($"|---|---|---|---|{string.Concat(LmeAbilities.All.Select(_ => "---|"))}---|---|---|---|---|");
        foreach (var s in Score(run).OrderByDescending(s => s.Accuracy))
        {
            var abilities = string.Join(" | ", LmeAbilities.All.Select(a => s.ByAbility.TryGetValue(a, out var v) ? $"{v:P0}" : "—"));
            text.AppendLine(c,
                $"| {s.System} | {s.Model} | {s.Questions} × {s.Repetitions} | {s.Accuracy:P1} (± {s.AccuracySpread:P1}) | {abilities} | {s.Errors} | {s.InputTokensPerQuestion:0} / {s.OutputTokensPerQuestion:0} | ${s.CostPerQuestionUsd:0.0000} | {(s.CostPerCorrectUsd is { } pc ? $"${pc:0.0000}" : "—")} | {s.MedianSeconds:0.0} |");
        }

        var versions = run.Results.Select(r => r.ModelVersion).OfType<string>().Distinct().ToList();
        if (versions.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(c, $"Agent model versions: {string.Join(", ", versions)}. Agent sessions run on a subscription: their cost is not in the dollars above.");
        }

        var errors = run.Results.Where(r => r.Error is not null).Take(20).ToList();
        if (errors.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Errors (first 20)");
            text.AppendLine();
            foreach (var e in errors)
            {
                text.AppendLine(c, $"- {e.System} · {e.Model} · {e.QuestionId} #{e.Repetition}: {e.Error!.ReplaceLineEndings(" ")[..Math.Min(200, e.Error.Length)]}");
            }
        }

        return text.ToString();
    }

    public static async Task<string> WriteAsync(LmeRunResult run, string outDir)
    {
        var folder = Path.Combine(outDir, DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "report.md"), Markdown(run));
        await File.WriteAllTextAsync(Path.Combine(folder, "results.json"),
            JsonSerializer.Serialize(new { run.Description, run.CostUsd, run.Stopped, run.Planned, run.SessionsUsed, Scores = Score(run), run.Results }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return folder;
    }
}
