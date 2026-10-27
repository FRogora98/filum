using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.Testing.Builders.Outcomes;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class HistoryToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task MemoryHistoryAndUndo_WhenAFileChangedTwice_ShouldListTheChangesAndTakeBackOnlyTheLatest()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "first\n", None));
        var second = Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "second", None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent);

        var history = (await Call(tools, "memory_history", new() { ["path"] = "/notes/a.md" })).Split('\n');
        var first = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None))[1].Id;
        var refused = await Call(tools, "memory_undo", new() { ["revisionId"] = first });
        var undone = await Call(tools, "memory_undo", new() { ["revisionId"] = second.RevisionId });

        Assert.Equal(2, history.Length);
        Assert.StartsWith($"- change {second.RevisionId}: Added 1 line · by the person · ", history[0]);
        Assert.StartsWith($"- change {first}: Created · by the agent · ", history[1]);
        Assert.StartsWith("Refused: /notes/a.md changed again after that", refused);
        Assert.StartsWith($"Undid change {second.RevisionId}", undone);
        Assert.Equal(["first"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        Assert.Equal(
            [(ToolStep.Read, "Read the history of /notes/a.md"), (ToolStep.Failed, $"Could not undo change {first}"), (ToolStep.Wrote, "Undid a change to /notes/a.md")],
            tools.Steps.Select(s => (s.Kind, s.Description)));
        Assert.Single(tools.Revisions);
    }
}
