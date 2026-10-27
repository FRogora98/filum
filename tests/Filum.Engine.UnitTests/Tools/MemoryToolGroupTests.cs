using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class MemoryToolGroupTests
{
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task Call_WhenTheTurnLimitIsReached_ShouldRefuseAndRecordOneStoppedStep()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions { MaxToolCallsPerTurn = 2 }, new JintCodeSandbox(), _user, Agent);

        await Call(tools, ToolNames.SkillList, []);
        await Call(tools, ToolNames.SkillList, []);
        var third = await Call(tools, ToolNames.SkillList, []);
        var fourth = await Call(tools, ToolNames.MemoryRead, new() { ["path"] = "/notes/a.md" });

        const string stop = "the limit of 2 memory actions for this message is reached; answer with what you have.";
        Assert.Equal($"Refused: {stop}", third);
        Assert.Equal($"Refused: {stop}", fourth);
        Assert.Equal(3, tools.Steps.Count);
        var stopped = tools.Steps[^1];
        Assert.Equal((ToolStep.Failed, ToolNames.SkillList, "Stopped: the limit of 2 memory actions was reached", stop), (stopped.Kind, stopped.Tool, stopped.Description, stopped.Error));
    }
}
