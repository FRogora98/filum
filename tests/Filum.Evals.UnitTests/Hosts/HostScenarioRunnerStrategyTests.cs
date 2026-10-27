using Filum.Evals.Hosts;
using Filum.Evals.Hosts.Wire;
using Filum.Evals.Scenarios;
using Filum.Evals.UnitTests.Fakes;
using System.Net;

namespace Filum.Evals.UnitTests.Hosts;

public sealed class HostScenarioRunnerStrategyTests
{
    private static readonly DateTimeOffset When = new(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);

    private static Scenario Scenario(string turns) => ScenarioLoader.Parse($$"""{"id":"s","language":"en","turns":[{{turns}}]}""", "s.json");

    private static SendMessageResponse Answer(string text, params StepDto[] steps) =>
        new(new ConversationDto(Guid.NewGuid(), "t", "m", "p", When, When),
            new MessageDto(Guid.NewGuid(), "user", "m", null, When, []),
            new MessageDto(Guid.NewGuid(), "assistant", text, "m1", When, steps),
            new UsageDto(100, 10, 0.001m));

    [Fact]
    public async Task RunAsync_WhenTheHostRegistersPeople_ShouldSendAsThePersonAndReadTheMemoryBack()
    {
        var core = new MemoryFileDetailDto(MemoryPaths.CorePath, "document", 9, 1, "normal", When, null, null, "Likes tea", new MemoryOriginDto("agent", null, When));
        var host = new StubHost()
            .On(HttpMethod.Post, "/api/accounts", new RegisteredSession("token-1"))
            .On(HttpMethod.Post, "/api/conversations/", Answer("Saved.", new StepDto(StepDto.Wrote, "memory_write", MemoryPaths.CorePath, "Wrote", 1, 1)))
            .On(HttpMethod.Get, "/api/memory/files", new[] { new MemoryFileDto(MemoryPaths.CorePath, "document", 9, 1, "normal", When, null, null) })
            .On(HttpMethod.Get, "/api/memory/file?path=", core);
        using var client = host.Client();
        var scenario = Scenario("""
            {"chat":"a","message":"I like tea","expect":[{"type":"wrote"},{"type":"core_contains","any":["tea"]}]},
            {"chat":"a","message":"Again","expect":[]},
            {"chat":"b","message":"New chat","expect":[]}
            """);

        var run = await new HostScenarioRunnerStrategy(client, new ScriptedJudge(), new HostTarget(Register: "/api/accounts")).RunAsync(scenario, "m1", 1, CancellationToken.None);

        Assert.True(run.Succeeded, string.Join("; ", run.Checks.Select(c => c.Result.Detail)));
        Assert.Empty(run.ClaimedNotDone);
        Assert.Equal(0.003m, run.CostUsd);
        Assert.Equal(3, run.Memories!.Count);
        Assert.Contains("@example.invalid", host.Requests[0].Body);
        var messages = host.Requests.Where(r => r.Path.EndsWith("/messages", StringComparison.Ordinal)).ToList();
        Assert.All(messages, m => Assert.Equal("Bearer token-1", m.Authorization));
        Assert.All(messages, m => Assert.Contains("\"model\":\"m1\"", m.Body));
        Assert.Equal(messages[0].Path, messages[1].Path);
        Assert.NotEqual(messages[1].Path, messages[2].Path);
    }

    [Fact]
    public async Task RunAsync_WhenTheHostRefusesTheTurn_ShouldKeepItsStatusAndCheckThatNoConversationWasSaved()
    {
        var host = new StubHost()
            .On(HttpMethod.Post, "/sample/conversations/", HttpStatusCode.PaymentRequired, "no messages left")
            .On(HttpMethod.Get, "/sample/conversations", Array.Empty<ConversationDto>());
        using var client = host.Client();
        var target = new HostTarget("/sample", Register: null, PersonHeader: "X-Person");

        var run = await new HostScenarioRunnerStrategy(client, new ScriptedJudge(), target)
            .RunAsync(Scenario("""{"chat":"a","message":"Hi","expect":[{"type":"refused","count":402},{"type":"core_contains","any":["x"]}]}"""), HostTarget.HostModel, 1, CancellationToken.None);

        var turn = Assert.Single(run.Turns);
        Assert.Equal(("402 no messages left", 402, false), (turn.Error, turn.Status, turn.ConversationExists));
        Assert.True(run.Checks[0].Result.Passed, run.Checks[0].Result.Detail);
        Assert.Equal("the host maps no memory group", run.Checks[1].Result.Detail);
        Assert.DoesNotContain(host.Requests, r => r.Path.Contains("register", StringComparison.Ordinal));
        Assert.DoesNotContain("\"model\"", host.Requests[0].Body!.Replace("\"model\":null", string.Empty, StringComparison.Ordinal));
    }
}
