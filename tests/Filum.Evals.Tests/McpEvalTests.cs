using System.Text.Json;

namespace Filum.Evals.Tests;

/// <summary>The growth test's logic (spec 018) with a fake agent: the two arms, the session cap, the agent's stream.</summary>
public sealed class McpEvalTests
{
    private const string Stream = """
        {"type":"system","subtype":"init","tools":["mcp__filum__memory_write"]}
        {"type":"assistant","message":{"content":[{"type":"text","text":"Let me save that."},{"type":"tool_use","id":"1","name":"mcp__filum__memory_overview","input":{}}]}}
        {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"1","content":"..."}]}}
        {"type":"assistant","message":{"content":[{"type":"tool_use","id":"2","name":"mcp__filum__memory_write","input":{"path":"/notes/a.md"}}]}}
        {"type":"result","subtype":"success","is_error":false,"result":"Saved it.","total_cost_usd":0.01}
        """;

    private static readonly Scenario TwoTurns = ScenarioLoader.Parse("""
        {"id":"remember","language":"en","turns":[
          {"chat":"a","message":"Remember: the key is under the mat.","expect":[{"type":"wrote"}]},
          {"chat":"b","message":"Where is the key?","expect":[{"type":"answer_contains","any":["mat"]}]}]}
        """, "remember.json");

    [Fact]
    public void The_agents_stream_gives_its_tools_and_its_answer()
    {
        var session = AgentStream.Parse(Stream);

        Assert.Equal(["mcp__filum__memory_overview", "mcp__filum__memory_write"], session.Tools);
        Assert.Equal("Saved it.", session.Answer);
        Assert.Null(session.Error);
        Assert.Equal([StepDto.Read, StepDto.Wrote], McpScenarioRunner.Steps(session.Tools).Select(s => s.Kind));
        Assert.Equal("the agent gave no result", AgentStream.Parse("not json\n").Error);
        Assert.StartsWith("the agent reported an error", AgentStream.Parse("""{"type":"result","is_error":true,"result":"boom"}""").Error);
    }

    [Fact]
    public void The_arm_with_filum_mounts_only_filum_mcp_on_its_folder_and_the_other_arm_nothing()
    {
        var runner = new McpScenarioRunner(new FakeAgent(), new ScriptedJudge(), "/tools/filum-mcp.dll", new SessionBudget(10));

        var with = JsonDocument.Parse(runner.McpConfig(withFilum: true, "/tmp/home")).RootElement.GetProperty("mcpServers");
        var without = JsonDocument.Parse(runner.McpConfig(withFilum: false, "/tmp/home")).RootElement.GetProperty("mcpServers");

        Assert.Equal(["filum"], with.EnumerateObject().Select(p => p.Name));
        Assert.Equal("dotnet", with.GetProperty("filum").GetProperty("command").GetString());
        Assert.Equal("/tools/filum-mcp.dll", with.GetProperty("filum").GetProperty("args")[0].GetString());
        Assert.Equal("/tmp/home", with.GetProperty("filum").GetProperty("env").GetProperty("FILUM_HOME").GetString());
        Assert.Empty(without.EnumerateObject());
    }

    [Fact]
    public async Task Each_turn_is_a_new_session_in_both_arms_and_the_report_puts_them_side_by_side()
    {
        var agent = new FakeAgent();
        var runner = new McpScenarioRunner(agent, new ScriptedJudge(), "filum-mcp", new SessionBudget(10));

        var result = await new EvalRun(runner, capUsd: 1).RunAsync([TwoTurns], [McpEvalCli.WithFilum, McpEvalCli.WithoutFilum], 1, CancellationToken.None);

        Assert.Equal(4, agent.Sessions.Count);
        Assert.Equal(4, agent.Sessions.Select(s => s.WorkingDirectory).Distinct().Count());
        Assert.Equal([true, true, false, false], agent.Sessions.Select(s => s.FilumMounted));
        var markdown = EvalReport.Markdown(result);
        Assert.Contains("| with filum |", markdown);
        Assert.Contains("| without |", markdown);
    }

    [Fact]
    public async Task The_session_cap_is_estimated_before_and_stops_the_run_during()
    {
        Assert.Equal(8, SessionBudget.Needed([TwoTurns, TwoTurns], repetitions: 1));

        var agent = new FakeAgent();
        var result = await new EvalRun(new McpScenarioRunner(agent, new ScriptedJudge(), "filum-mcp", new SessionBudget(3)), capUsd: 1)
            .RunAsync([TwoTurns], [McpEvalCli.WithFilum, McpEvalCli.WithoutFilum], 1, CancellationToken.None);

        Assert.True(result.StoppedAtCap);
        Assert.Single(result.Runs);
        Assert.Equal(2, agent.Sessions.Count);
    }

    private sealed class FakeAgent : IAgent
    {
        public string Name => "fake agent";

        public List<(string Message, string WorkingDirectory, bool FilumMounted)> Sessions { get; } = [];

        public Task<AgentSession> AskAsync(string message, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
        {
            Sessions.Add((message, workingDirectory, filumMounted));
            return Task.FromResult(new AgentSession(filumMounted ? "It is under the mat." : "I don't know.", filumMounted ? ["mcp__filum__memory_write"] : [], null));
        }
    }
}
