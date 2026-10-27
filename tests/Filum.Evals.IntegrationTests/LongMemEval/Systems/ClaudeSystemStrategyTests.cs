using Filum.Evals.LongMemEval.Systems;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.IntegrationTests.LongMemEval.Systems;

public sealed class ClaudeSystemStrategyTests
{
    [Fact]
    public async Task AnswerAsync_WhenWithFilum_ShouldTakeOneSessionPerHistorySessionAndOneForTheQuestion()
    {
        var agent = new PromptAgent();
        var instance = (await LongMemEvalInstances.LoadAsync())[0];
        var withFilum = new ClaudeSystemStrategy(agent, ClaudeSystemMode.WithFilum, "filum-mcp", "sonnet");
        var full = new ClaudeSystemStrategy(agent, ClaudeSystemMode.FullContext, "filum-mcp", "sonnet");

        Assert.Equal(3, withFilum.SessionsFor(instance));
        await withFilum.AnswerAsync(instance, CancellationToken.None);
        Assert.Equal([true, true, true], agent.Sessions.Select(s => s.Filum));
        Assert.Contains("bike", agent.Sessions[0].Message);
        Assert.StartsWith("Today is", agent.Sessions[2].Message);

        agent.Sessions.Clear();
        await full.AnswerAsync(instance, CancellationToken.None);
        var only = Assert.Single(agent.Sessions);
        Assert.False(only.Filum);
        Assert.Contains("I bought a boat today.", only.Message);
    }

    [Fact]
    public async Task AnswerAsync_WhenWithFilumAndConsolidation_ShouldTidyTheMemoryAfterEachHistorySession()
    {
        var agent = new PromptAgent();
        var instance = (await LongMemEvalInstances.LoadAsync())[0];
        var system = new ClaudeSystemStrategy(agent, ClaudeSystemMode.WithFilum, "filum-mcp", "haiku", consolidate: true);

        Assert.Equal(5, system.SessionsFor(instance));
        await system.AnswerAsync(instance, CancellationToken.None);

        Assert.Equal(5, agent.Sessions.Count);
        Assert.All(agent.Sessions, s => Assert.True(s.Filum));
        Assert.Equal(ClaudeSystemStrategy.ConsolidatePrompt, agent.Sessions[1].Message);
        Assert.Equal(ClaudeSystemStrategy.ConsolidatePrompt, agent.Sessions[3].Message);
        Assert.StartsWith("Today is", agent.Sessions[4].Message);
    }
}
