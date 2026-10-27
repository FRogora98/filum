using Filum.Evals.Scenarios;
using Filum.Evals.Runs.Contracts;

namespace Filum.Evals.Runs;

// Until done or until the spending cap is reached.
internal sealed class EvalRun(IScenarioRunnerStrategy runner, decimal capUsd, TextWriter? progress = null, string? target = null)
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
