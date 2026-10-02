using Filum.Agent.Tests.Infrastructure;

namespace Filum.Agent.Tests;

/// <summary>What a turn sends (spec 031): no tool whose content the instructions already hold, and the rules for changing facts.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LeanTurnTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_turn_is_not_offered_the_overview_and_is_told_how_to_read_changing_facts()
    {
        var llm = new FakeChatClient();
        await using var host = new SampleHostFactory(postgres, llm);

        var response = await host.Person(Guid.NewGuid()).Send(Guid.NewGuid(), "Hello");

        Assert.True(response.IsSuccessStatusCode);
        var offered = llm.Options[0]!.Tools!.Select(t => t.Name).ToList();
        Assert.DoesNotContain("memory_overview", offered);
        Assert.Equal(MemoryTools.Catalog(new MemoryOptions()).Select(t => t.Name).Where(n => n != "memory_overview"), offered.Where(n => n != "sample_now"));
        Assert.Contains("never from the first thing you find", llm.InstructionsOf(0));
        Assert.Contains("work out the new value from the last one", llm.InstructionsOf(0));
    }
}
