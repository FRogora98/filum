using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Instructions;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.UnitTests.Memory.Core;

public sealed class CoreBootstrapperTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly IMemoryService _memory = MemoryServices.InMemory();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task EnsureLearnedAsync_WhenTheMemoryIsNew_ShouldCreateAnEmptyFileOutOfTheMapWhoseLinesTheInstructionsCarry()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Assert.Equal("(nothing yet)", await _memory.EnsureLearnedAsync(_user, None));
        Assert.Equal(MemoryAuthor.Platform, (await _memory.GetHistoryAsync(_user, LearnedFile.Path, None)).Value!.Single().Author);

        await _memory.AppendAsync(_user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()), LearnedFile.Path, "- 2026-03-01: calls their partner Gio", None);

        Assert.Equal("- 2026-03-01: calls their partner Gio", await _memory.EnsureLearnedAsync(_user, None));
        Assert.DoesNotContain(LearnedFile.Path, await _memory.BuildIndexAsync(_user, None));
        Assert.Contains("# What you have learned about the person (/learned.md: inferred, never above the core)", PlatformInstructions.LearnedSection("x"));
    }
}
