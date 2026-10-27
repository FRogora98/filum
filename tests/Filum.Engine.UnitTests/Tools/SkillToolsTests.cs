using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.Testing.Builders.Outcomes;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class SkillToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task SkillList_WhenCalled_ShouldGiveEverySkillWithItsStateAndPrivateOnesOnlyWhenAsked()
    {
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("log-note", "Keep a note", "note that", true, "1. Add it."), false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("old-way", "An old way", "never", true, "1. Nothing."), false, None));
        Ok(await _memory.SetSkillEnabledAsync(_user, Agent, "old-way", false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("hidden-one", "Hidden", "rarely", true, "1. Shh."), false, None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/skills/hidden-one.md", MemorySensitivity.Private, None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent);

        var listed = await Call(tools, "skill_list", []);
        var all = await Call(tools, "skill_list", new() { ["includePrivate"] = true });

        Assert.Equal(["- /log-note: Keep a note · when note that · on", "- /old-way: An old way · when never · off"], listed.Split('\n'));
        Assert.Contains("- /hidden-one: Hidden · when rarely · on · private", all);
    }
}
