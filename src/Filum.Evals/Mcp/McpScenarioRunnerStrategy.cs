using Filum.Evals.Checks;
using Filum.Evals.Judging;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;
using System.Diagnostics;
using Filum.Evals.Mcp.Contracts;
using Filum.Evals.Runs.Contracts;

namespace Filum.Evals.Mcp;

// Every chat of a scenario is one agent session in its own folder, continued turn by turn (spec 056); the "model" of a
// run is the arm.
internal sealed class McpScenarioRunnerStrategy(IAgentStrategy agent, IClaimJudge judge, string filumMcp, SessionBudget budget, bool hook = false) : IScenarioRunnerStrategy
{
    private const string TemporaryFolder = "filum-evals-mcp";

    private readonly McpConfigWriter _config = new(filumMcp, hook);
    private readonly TurnAssessor _assessor = new(judge);

    public bool CanStart(Scenario scenario) => budget.CanStart(scenario.TurnCount);

    public async Task<ScenarioRun> RunAsync(Scenario scenario, string model, int repetition, CancellationToken cancellationToken)
    {
        var arm = model;
        var withFilum = arm == McpArms.WithFilum;
        var folder = RunFolder.Create(TemporaryFolder);
        var home = Path.Combine(folder.FullPath, EvalsConstants.Folders.Memory);
        var config = await _config.WriteConfigAsync(folder.FullPath, withFilum, home, cancellationToken);
        try
        {
            var recorder = new ScenarioRunRecorder();
            var chats = new Dictionary<string, AgentChat>(StringComparer.Ordinal);
            for (var i = 0; i < scenario.Turns.Count; i++)
            {
                var step = scenario.Turns[i];
                var chat = chats.TryGetValue(step.Chat, out var known) ? known : new AgentChat(Guid.NewGuid(), Path.Combine(folder.FullPath, $"chat-{chats.Count + 1}"));
                var turn = await PlayAsync(step, i + 1, chat, config, withFilum, cancellationToken);
                chats[step.Chat] = chat with { Started = true };
                var memory = withFilum ? await FilumMemoryReader.ReadAsync(home, cancellationToken) : new MemorySnapshot([]);
                recorder.Record(turn, memory, await _assessor.AssessAsync(step, turn, memory, recorder.Turns, cancellationToken));
            }

            return recorder.ToRun(scenario.Id, arm, repetition, withMemories: false);
        }
        finally
        {
            folder.Delete();
        }
    }

    // Nothing of the agent's own carries over between chats.
    private async Task<TurnRecord> PlayAsync(ScenarioTurn step, int index, AgentChat chat, string config, bool withFilum, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(chat.Folder);
        var clock = Stopwatch.StartNew();
        budget.Spend();
        var session = await agent.AskAsync(step.Message, chat, chat.Folder, config, withFilum, cancellationToken);
        return new TurnRecord(index, step.Chat, step.Message, session.Answer, AgentToolSteps.Of(session.Calls), 0, 0, 0m, clock.Elapsed.TotalSeconds, session.Error,
            AgentToolSteps.Proposal(session.Calls));
    }
}
