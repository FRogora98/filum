using Filum.Evals.Checks;
using Filum.Evals.Hosts.Wire;
using Filum.Evals.Mcp;

namespace Filum.Evals.UnitTests.Mcp;

public sealed class AgentOutputTests
{
    private const string Stream = """
        {"type":"system","subtype":"init","tools":["mcp__filum__memory_write"]}
        {"type":"assistant","message":{"content":[{"type":"text","text":"Let me save that."},{"type":"tool_use","id":"1","name":"mcp__filum__memory_overview","input":{}}]}}
        {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"1","content":"..."}]}}
        {"type":"assistant","message":{"content":[{"type":"tool_use","id":"2","name":"mcp__filum__memory_write","input":{"path":"/notes/a.md"}}]}}
        {"type":"result","subtype":"success","is_error":false,"result":"Saved it.","total_cost_usd":0.01}
        """;

    [Fact]
    public void Parse_WhenGivenTheAgentsStream_ShouldGiveItsToolsAndItsAnswer()
    {
        var session = AgentOutput.Parse(Stream);

        Assert.Equal(["mcp__filum__memory_overview", "mcp__filum__memory_write"], session.Tools);
        Assert.Equal("Saved it.", session.Answer);
        Assert.Null(session.Error);
        Assert.Equal([StepDto.Read, StepDto.Wrote], AgentToolSteps.Of(session.Tools).Select(s => s.Kind));
        Assert.Equal([StepDto.Wrote, StepDto.Read], AgentToolSteps.Of(["mcp__filum__fact_record", "mcp__filum__facts_current"]).Select(s => s.Kind));
        Assert.Equal("the agent gave no result", AgentOutput.Parse("not json\n").Error);
        Assert.StartsWith("the agent reported an error", AgentOutput.Parse("""{"type":"result","is_error":true,"result":"boom"}""").Error);
    }

    [Fact]
    public void Arguments_WhenAChatStartsAndWhenItContinues_ShouldGiveItsSessionIdThenResumeIt()
    {
        var agent = new ClaudeCodeAgentStrategy(model: null);
        var id = Guid.NewGuid();

        var first = agent.Arguments("mcp.json", filumMounted: true, new AgentChat(id, "chat-1")).ToList();
        var next = agent.Arguments("mcp.json", filumMounted: true, new AgentChat(id, "chat-1", Started: true)).ToList();

        Assert.Equal(id.ToString(), first[first.IndexOf("--session-id") + 1]);
        Assert.DoesNotContain("--resume", first);
        Assert.Equal(id.ToString(), next[next.IndexOf("--resume") + 1]);
        Assert.DoesNotContain("--session-id", next);
        Assert.DoesNotContain("--resume", agent.Arguments("mcp.json", filumMounted: true, chat: null));
    }

    [Fact]
    public void Of_WhenTheAgentComputesUsesAndProposesSkillsOrEndsAFact_ShouldGiveTheStepsAHostGives()
    {
        var session = AgentOutput.Parse("""
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"1","name":"mcp__filum__collection_aggregate","input":{"path":"/runs.csv"}},{"type":"tool_use","id":"2","name":"mcp__filum__compute_run","input":{}}]}}
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"3","name":"mcp__filum__skill_use","input":{"name":"log-note"}},{"type":"tool_use","id":"4","name":"mcp__filum__fact_end","input":{}}]}}
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"5","name":"mcp__filum__skill_propose","input":{"name":"hardware-list","description":"Adds to the list.","when":"Things to buy.","steps":"1. Add it."}}]}}
            {"type":"result","is_error":false,"result":"Done."}
            """);

        var steps = AgentToolSteps.Of(session.Calls);

        Assert.Equal([StepDto.Computed, StepDto.Computed, StepDto.Read, StepDto.Wrote, StepDto.Read], steps.Select(s => s.Kind));
        Assert.Equal("Used skill /log-note", steps[2].Description);
        Assert.True(new TurnRecord(1, "a", "m", "Done.", steps, 0, 0, 0m, 0).UsedSkill);
        var proposal = AgentToolSteps.Proposal(session.Calls);
        Assert.Equal(("hardware-list", "Adds to the list.", "Things to buy.", "1. Add it."), (proposal?.Name, proposal?.Description, proposal?.When, proposal?.Steps));
        Assert.Null(AgentToolSteps.Proposal([new AgentToolCall("mcp__filum__skill_use")]));
    }
}
