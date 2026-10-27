using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Consolidation;
using Filum.Engine.Instructions;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class ProposalToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly IMemoryService _memory = MemoryServices.InMemory();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task MemoryPropose_WhenAPassNeedsANewFile_ShouldProposeItAndCreateNothingUntilThePersonAccepts()
    {
        var pass = Pass();

        var refused = await Call(pass, "collection_add_rows", new() { ["path"] = "/plants.csv", ["rows"] = new[] { new Dictionary<string, object?> { ["name"] = "fern" } } });
        var proposed = await Call(pass, ConsolidationPrompt.ProposeTool, new() { ["proposal"] = "A collection /plants.csv for the plants the person keeps.", ["sources"] = new long[] { 3 } });
        var again = await Call(pass, ConsolidationPrompt.ProposeTool, new() { ["proposal"] = "A collection /plants.csv for the plants the person keeps." });

        Assert.Contains("cannot create files", refused);
        Assert.StartsWith("Proposed (proposal ", proposed);
        Assert.Contains("already waiting", again);
        Assert.Empty((await _memory.ListAsync(_user, "/", true, None)).Value!);
        var open = Assert.Single(await _memory.OpenProposalsAsync(_user, None));
        Assert.Equal([3L], open.Sources);
        Assert.Contains($"- proposal {open.Id}: A collection /plants.csv", PlatformInstructions.Proposals([open]));

        var turn = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
        var answer = await Call(turn, "proposal_answer", new() { ["id"] = open.Id, ["accepted"] = true });
        Assert.Contains("accepted: now create", answer);
        Assert.Empty(await _memory.OpenProposalsAsync(_user, None));
        Assert.Contains("no open proposal", await Call(turn, "proposal_answer", new() { ["id"] = open.Id, ["accepted"] = false }));
    }

    private MemoryTools Pass() => new(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Consolidation, consolidation: true);
}
