using Filum.Evals.Hosts;
using Filum.Evals.Hosts.Wire;
using Filum.Evals.LongMemEval.Systems;
using Filum.Evals.UnitTests.Builders;
using Filum.Evals.UnitTests.Fakes;

namespace Filum.Evals.UnitTests.LongMemEval.Systems;

public sealed class HostSystemStrategyTests
{
    private static readonly DateTimeOffset When = new(2026, 1, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnswerAsync_WhenConsolidating_ShouldSendEverySessionAPassAfterEachAndCountWhatAllCost()
    {
        var answer = new SendMessageResponse(new ConversationDto(Guid.NewGuid(), "t", "m", "p", When, When),
            new MessageDto(Guid.NewGuid(), "user", "m", null, When, []), new MessageDto(Guid.NewGuid(), "assistant", "The bike.", null, When, []), new UsageDto(10, 2, 0.01m));
        var host = new StubHost()
            .On(HttpMethod.Post, "/api/conversations/", answer)
            .On(HttpMethod.Post, "/api/memory/consolidate", new ConsolidationPass(1, 1, 0, new UsageDto(5, 1, 0.002m)));
        using var client = host.Client();
        var instance = (await LongMemEvalInstances.LoadAsync())[0];

        var result = await new HostSystemStrategy(client, new HostTarget(PersonHeader: "X-Person"), HostTarget.HostModel, "filum", consolidate: true)
            .AnswerAsync(instance, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("The bike.", result.Answer);
        Assert.Equal(3, result.Turns);
        Assert.Equal((3 * 10 + 2 * 5, 3 * 2 + 2 * 1, 0.034m), (result.InputTokens, result.OutputTokens, result.CostUsd));
        Assert.Equal(2, host.Requests.Count(r => r.Path == "/api/memory/consolidate"));
        Assert.Equal(3, host.Requests.Select(r => r.Path).Where(p => p.EndsWith("/messages", StringComparison.Ordinal)).Distinct().Count());
    }

    [Fact]
    public async Task AnswerAsync_WhenASessionFails_ShouldStopWithTheHostsErrorAndWhatWasSpent()
    {
        var host = new StubHost().On(HttpMethod.Post, "/api/conversations/", System.Net.HttpStatusCode.BadGateway, "model down");
        using var client = host.Client();
        var instance = (await LongMemEvalInstances.LoadAsync())[0];

        var result = await new HostSystemStrategy(client, new HostTarget(PersonHeader: "X-Person"), "m1", "filum").AnswerAsync(instance, CancellationToken.None);

        Assert.Equal("session of 2023/04/10 (Mon) 14:47: 502 model down", result.Error);
        Assert.Equal(1, result.Turns);
    }
}
