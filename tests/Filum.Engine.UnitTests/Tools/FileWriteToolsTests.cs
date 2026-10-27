using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Memory.Core;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class FileWriteToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task Write_WhenThePathIsACollection_ShouldRefuseAndNameTheCollectionTools()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent);

        var refused = await Call(tools, ToolNames.MemoryWrite, new() { ["path"] = "/books.csv", ["content"] = "title\nDune\n" });

        Assert.Equal("Refused: /books.csv is a collection: use collection_add_rows, collection_update_rows or collection_remove_rows; the platform writes the file.", refused);
        var step = Assert.Single(tools.Steps);
        Assert.Equal((ToolStep.Failed, "Could not write /books.csv"), (step.Kind, step.Description));
        Assert.Empty(tools.Revisions);
    }

    [Fact]
    public async Task Append_WhenItSucceeds_ShouldKeepTheRevisionAndDescribeTheChange()
    {
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent);

        var done = await Call(tools, ToolNames.MemoryAppend, new() { ["path"] = "/notes/a.md", ["text"] = "one\ntwo\n" });

        Assert.Equal("Created /notes/a.md with 2 lines.", done);
        var step = Assert.Single(tools.Steps);
        Assert.Equal((ToolStep.Wrote, "/notes/a.md", "Created /notes/a.md with 2 lines"), (step.Kind, step.Path, step.Description));
        Assert.Equal([step.RevisionId!.Value], tools.Revisions);
    }

    [Fact]
    public async Task MemoryAppendAndEdit_WhenAPassRuns_ShouldLeaveAloneWhatThePersonChangedLastAndAddNoSectionToTheCore()
    {
        await _memory.EnsureCoreAsync(_user, None);
        await _memory.WriteAsync(_user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()), "/notes/trip.md", "# Trip\n", None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/notes/mine.md", "# Mine\nAs I wrote it.\n", None);
        var pass = Pass();

        var mine = await Call(pass, "memory_append", new() { ["path"] = "/notes/mine.md", ["text"] = "2026-03-01: more" });
        var trip = await Call(pass, "memory_append", new() { ["path"] = "/notes/trip.md", ["text"] = "2026-03-01: leaves early" });
        var section = await Call(pass, "memory_edit", new() { ["path"] = "/filum.md", ["oldText"] = "# Rules\n", ["newText"] = "# Rules\n\n# Pets\n" });
        var appended = await Call(pass, "memory_append", new() { ["path"] = "/filum.md", ["text"] = "# Pets\nA cat." });
        var line = await Call(pass, "memory_edit", new() { ["path"] = "/filum.md", ["oldText"] = "# About you\n", ["newText"] = "# About you\nLives by the sea.\n" });

        Assert.Contains("changed /notes/mine.md themselves last", mine);
        Assert.StartsWith("Added 1 line", trip);
        Assert.Contains("does not add sections to the core", section);
        Assert.Contains("does not add sections to the core", appended);
        Assert.StartsWith("Edited /filum.md", line);
        var history = (await _memory.GetHistoryAsync(_user, "/notes/trip.md", None)).Value!;
        Assert.Equal(MemoryAuthor.Consolidation, history[0].Author);
    }

    [Fact]
    public async Task MemoryAppendAndEdit_WhenThePersonEditedTheLearnedFile_ShouldLetAPassAddToItButNotRewriteIt()
    {
        await _memory.EnsureLearnedAsync(_user, None);
        await _memory.AppendAsync(_user, MemoryActor.Person, LearnedFile.Path, "- 2026-03-01: likes short answers", None);
        var pass = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Consolidation, consolidation: true);

        var rewritten = await Call(pass, "memory_edit", new() { ["path"] = LearnedFile.Path, ["oldText"] = "likes short answers", ["newText"] = "likes long answers" });
        var added = await Call(pass, "memory_append", new() { ["path"] = LearnedFile.Path, ["text"] = "- 2026-03-02: walks every morning (events 4, 9)" });

        Assert.StartsWith("Added 1 line", added);
        Assert.Contains("changed /learned.md themselves last", rewritten);
    }

    private MemoryTools Pass() => new(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Consolidation, consolidation: true);
}
