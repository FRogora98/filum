using Filum.Evals.Judging;
using System.Globalization;
using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.LongMemEval;

// With a journal every answer is written the moment it is judged, and answers already in it are not played again: a run
// that stops resumes where it stopped. The caps count only what this run spends.
internal sealed class LongMemEvalRun(IPromptJudge judge, decimal capUsd, int maxSessions = int.MaxValue, TextWriter? progress = null, string description = "", string? journal = null)
{
    public async Task<LongMemEvalRunResult> RunAsync(IReadOnlyList<LongMemEvalInstance> instances, IReadOnlyList<ILongMemEvalSystemStrategy> systems, int repetitions, CancellationToken cancellationToken)
    {
        var results = journal is null ? new List<LongMemEvalResult>() : LongMemEvalJournal.Read(journal).ToList();
        var done = results.Select(r => (r.System, r.Model, r.QuestionId, r.Repetition)).ToHashSet();
        var spent = 0m;
        var sessions = 0;
        var planned = instances.Count * systems.Count * repetitions;
        for (var repetition = 1; repetition <= repetitions; repetition++)
        {
            foreach (var system in systems)
            {
                foreach (var instance in instances.Where(i => !done.Contains((system.Name, system.Model, i.QuestionId, repetition))))
                {
                    var needed = system.SessionsFor(instance);
                    if (spent >= capUsd || sessions + needed > maxSessions)
                    {
                        progress?.WriteLine(spent >= capUsd
                            ? $"Stopped: the cap of ${capUsd} is reached after {results.Count} of {planned}."
                            : $"Stopped: no agent sessions left after {results.Count} of {planned}.");
                        return new LongMemEvalRunResult(results, spent, true, planned, sessions, description);
                    }

                    sessions += needed;
                    var result = await PlayAsync(system, instance, repetition, cancellationToken);
                    results.Add(result);
                    spent += result.CostUsd + result.JudgeCostUsd;
                    Report(result, results.Count, planned, spent);
                }
            }
        }

        return new LongMemEvalRunResult(results, spent, false, planned, sessions, description);
    }

    // Written to the journal at once.
    private async Task<LongMemEvalResult> PlayAsync(ILongMemEvalSystemStrategy system, LongMemEvalInstance instance, int repetition, CancellationToken cancellationToken)
    {
        var answer = await system.AnswerAsync(instance, cancellationToken);
        var (correct, judgeCost) = answer.Error is null && answer.Answer.Length > 0
            ? await judge.YesAsync(LongMemEvalJudge.Prompt(instance, answer.Answer), cancellationToken)
            : (false, 0m);
        var result = new LongMemEvalResult(system.Name, system.Model, instance.QuestionId, instance.Ability, repetition, correct, answer.Answer,
            answer.InputTokens, answer.OutputTokens, answer.CostUsd, judgeCost, answer.Seconds, answer.Turns, answer.Error, answer.ModelVersion);
        if (journal is not null)
        {
            await LongMemEvalJournal.AppendAsync(journal, result, cancellationToken);
        }

        return result;
    }

    private void Report(LongMemEvalResult result, int count, int planned, decimal spent) =>
        progress?.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[{count}/{planned}] {result.System} · {result.Model} · {result.QuestionId} ({result.Ability}) #{result.Repetition}: {(result.Error is not null ? "error" : result.Correct ? "correct" : "wrong")} · ${spent:0.0000}"));
}
