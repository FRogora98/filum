using Filum.Evals.Scenarios;

namespace Filum.Evals.Runs.Contracts;

/// <summary>Plays one scenario once; a runner with a budget of its own (agent sessions) says when it cannot start another.</summary>
internal interface IScenarioRunnerStrategy
{
    Task<ScenarioRun> RunAsync(Scenario scenario, string model, int repetition, CancellationToken cancellationToken);

    bool CanStart(Scenario scenario) => true;
}
