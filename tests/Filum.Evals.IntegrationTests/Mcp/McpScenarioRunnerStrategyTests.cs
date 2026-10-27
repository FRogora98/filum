using Filum.Evals.Mcp;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.IntegrationTests.Mcp;

public sealed class McpScenarioRunnerStrategyTests
{
    private static readonly Scenario TwoTurns = ScenarioLoader.Parse("""
        {"id":"remember","language":"en","turns":[
          {"chat":"a","message":"Remember: the key is under the mat.","expect":[{"type":"wrote"}]},
          {"chat":"b","message":"Where is the key?","expect":[{"type":"answer_contains","any":["mat"]}]}]}
        """, "remember.json");

    private static readonly Scenario TwoChats = ScenarioLoader.Parse("""
        {"id":"follow-up","language":"en","turns":[
          {"chat":"a","message":"Add milk to my list.","expect":[]},
          {"chat":"a","message":"Yes, save it.","expect":[]},
          {"chat":"b","message":"What is on my list?","expect":[]}]}
        """, "follow-up.json");

    [Fact]
    public async Task RunAsync_WhenTwoTurnsShareAChat_ShouldContinueItsSessionAndStartANewOneForAnotherChat()
    {
        var agent = new FakeAgent();
        var runner = new McpScenarioRunnerStrategy(agent, new ScriptedJudge(), "filum-mcp", new SessionBudget(10));

        await new EvalRun(runner, capUsd: 1).RunAsync([TwoChats], [McpArms.WithFilum], 1, CancellationToken.None);

        var chats = agent.Sessions.Select(s => s.Chat!).ToList();
        Assert.Equal([false, true, false], chats.Select(c => c.Started));
        Assert.Equal(chats[0].SessionId, chats[1].SessionId);
        Assert.NotEqual(chats[0].SessionId, chats[2].SessionId);
        Assert.Equal(agent.Sessions[0].WorkingDirectory, agent.Sessions[1].WorkingDirectory);
        Assert.NotEqual(agent.Sessions[0].WorkingDirectory, agent.Sessions[2].WorkingDirectory);
    }

    [Fact]
    public async Task RunAsync_WhenPlayingBothArms_ShouldMakeEachChatANewSessionAndReportThemSideBySide()
    {
        var agent = new FakeAgent();
        var runner = new McpScenarioRunnerStrategy(agent, new ScriptedJudge(), "filum-mcp", new SessionBudget(10));

        var result = await new EvalRun(runner, capUsd: 1).RunAsync([TwoTurns], [McpArms.WithFilum, McpArms.WithoutFilum], 1, CancellationToken.None);

        Assert.Equal(4, agent.Sessions.Count);
        Assert.Equal(4, agent.Sessions.Select(s => s.WorkingDirectory).Distinct().Count());
        Assert.Equal([true, true, false, false], agent.Sessions.Select(s => s.FilumMounted));
        var markdown = EvalReport.Markdown(result, TimeProvider.System);
        Assert.Contains("| with filum |", markdown);
        Assert.Contains("| without |", markdown);
    }

    [Fact]
    public async Task RunAsync_WhenTheSessionCapIsReached_ShouldHaveEstimatedItBeforeAndStopTheRun()
    {
        Assert.Equal(8, SessionBudget.Needed([TwoTurns, TwoTurns], repetitions: 1));

        var agent = new FakeAgent();
        var result = await new EvalRun(new McpScenarioRunnerStrategy(agent, new ScriptedJudge(), "filum-mcp", new SessionBudget(3)), capUsd: 1)
            .RunAsync([TwoTurns], [McpArms.WithFilum, McpArms.WithoutFilum], 1, CancellationToken.None);

        Assert.True(result.StoppedAtCap);
        Assert.Single(result.Runs);
        Assert.Equal(2, agent.Sessions.Count);
    }
}
