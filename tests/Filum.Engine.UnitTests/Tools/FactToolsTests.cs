using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Facts;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class FactToolsTests
{
    private readonly IMemoryService _memory = MemoryServices.InMemory();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task FactRecord_WhenCalledInATurn_ShouldCiteItsMessageWhileTheCollectionToolsLeaveTheFactsAlone()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()), [42]);

        var recorded = await Call(tools, "fact_record", new() { ["subject"] = "the person", ["attribute"] = "plants", ["value"] = "3" });
        var refused = await Call(tools, "collection_add_rows", new() { ["path"] = FactTable.Path, ["rows"] = new[] { new Dictionary<string, object?> { ["subject"] = "x" } } });
        var current = await Call(tools, "facts_current", new());

        Assert.StartsWith("Recorded: the person · plants is 3", recorded);
        Assert.Contains("use fact_record", refused);
        Assert.Contains($"- the person · plants: 3 (since {FactTable.Day(DateTimeOffset.UtcNow)} by the agent; from event 42)", current);
        Assert.Equal(["Recorded the person · plants: 3", "Could not add rows to /facts.csv", "Read the current facts"], tools.Steps.Select(s => s.Description));
    }

    [Fact]
    public async Task FactsCurrent_WhenAValueIsASubjectWithFactsOfItsOwn_ShouldGiveThemOneStepOn()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
        await Call(tools, "fact_record", new() { ["subject"] = "Book Seven", ["attribute"] = "author", ["value"] = "Ada Brook" });
        await Call(tools, "fact_record", new() { ["subject"] = "Ada Brook", ["attribute"] = "spouse", ["value"] = "River Okafor" });
        await Call(tools, "fact_record", new() { ["subject"] = "River Okafor", ["attribute"] = "birth city", ["value"] = "Lisbon" });

        var current = await Call(tools, "facts_current", new() { ["subject"] = "Book Seven" });

        Assert.Contains("- Book Seven · author: Ada Brook", current);
        Assert.Contains("Facts that hold now about Ada Brook (the value of Book Seven · author):", current);
        Assert.Contains("- Ada Brook · spouse: River Okafor", current);
        Assert.DoesNotContain("Lisbon", current);
    }

    [Fact]
    public async Task FactsCurrent_WhenNoSubjectIsGiven_ShouldListTheFactsAlone()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
        await Call(tools, "fact_record", new() { ["subject"] = "Book Seven", ["attribute"] = "author", ["value"] = "Ada Brook" });
        await Call(tools, "fact_record", new() { ["subject"] = "Ada Brook", ["attribute"] = "spouse", ["value"] = "River Okafor" });

        Assert.DoesNotContain("Facts that hold now about", await Call(tools, "facts_current", new()));
    }
}
