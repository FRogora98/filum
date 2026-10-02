using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tests;

/// <summary>What a consolidation pass may and may not do (spec 030), decided in code.</summary>
public sealed class ConsolidationRulesTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private readonly MemoryService _memory = new(new InMemoryMemoryStore(), Options.Create(new MemoryOptions()), NullLogger<MemoryService>.Instance);
    private readonly Guid _user = Guid.NewGuid();

    private MemoryTools Pass() => new(_memory, new MemoryOptions(), _user, MemoryActor.Consolidation, consolidation: true);

    [Fact]
    public void A_pass_gets_only_its_tools_and_a_turn_never_gets_the_proposing_one()
    {
        Assert.Equal(Consolidation.Tools.Order(), Pass().Tools.Select(t => t.Name).Order());
        Assert.DoesNotContain(MemoryTools.Catalog(new MemoryOptions()), t => t.Name == Consolidation.ProposeTool);
    }

    [Fact]
    public async Task A_pass_cannot_create_a_file_it_proposes_it_and_nothing_exists_until_the_person_accepts()
    {
        var pass = Pass();

        var refused = await Call(pass, "collection_add_rows", new() { ["path"] = "/plants.csv", ["rows"] = new[] { new Dictionary<string, object?> { ["name"] = "fern" } } });
        var proposed = await Call(pass, Consolidation.ProposeTool, new() { ["proposal"] = "A collection /plants.csv for the plants the person keeps.", ["sources"] = new long[] { 3 } });
        var again = await Call(pass, Consolidation.ProposeTool, new() { ["proposal"] = "A collection /plants.csv for the plants the person keeps." });

        Assert.Contains("cannot create files", refused);
        Assert.StartsWith("Proposed (proposal ", proposed);
        Assert.Contains("already waiting", again);
        Assert.Empty((await _memory.ListAsync(_user, "/", true, None)).Value!);
        var open = Assert.Single(await _memory.OpenProposalsAsync(_user, None));
        Assert.Equal([3L], open.Sources);
        Assert.Contains($"- proposal {open.Id}: A collection /plants.csv", PlatformInstructions.Proposals([open]));

        var turn = new MemoryTools(_memory, new MemoryOptions(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));
        var answer = await Call(turn, "proposal_answer", new() { ["id"] = open.Id, ["accepted"] = true });
        Assert.Contains("accepted: now create", answer);
        Assert.Empty(await _memory.OpenProposalsAsync(_user, None));
        Assert.Contains("no open proposal", await Call(turn, "proposal_answer", new() { ["id"] = open.Id, ["accepted"] = false }));
    }

    [Fact]
    public async Task A_pass_leaves_alone_what_the_person_changed_last_and_adds_no_section_to_the_core()
    {
        await _memory.EnsureCoreAsync(_user, None);
        await _memory.WriteAsync(_user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()), "/notes/trip.md", "# Trip\n", None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/notes/mine.md", "# Mine\nAs I wrote it.\n", None);
        var pass = Pass();

        var mine = await Call(pass, "memory_append", new() { ["path"] = "/notes/mine.md", ["text"] = "2026-03-01: more" });
        var trip = await Call(pass, "memory_append", new() { ["path"] = "/notes/trip.md", ["text"] = "2026-03-01: leaves early" });
        var section = await Call(pass, "memory_edit", new() { ["path"] = "/filum.md", ["oldText"] = "# Rules\n", ["newText"] = "# Rules\n\n# Pets\n" });
        var line = await Call(pass, "memory_edit", new() { ["path"] = "/filum.md", ["oldText"] = "# About you\n", ["newText"] = "# About you\nLives by the sea.\n" });

        Assert.Contains("changed /notes/mine.md themselves last", mine);
        Assert.StartsWith("Added 1 line", trip);
        Assert.Contains("does not add sections to the core", section);
        Assert.StartsWith("Edited /filum.md", line);
        var history = (await _memory.GetHistoryAsync(_user, "/notes/trip.md", None)).Value!;
        Assert.Equal(MemoryAuthor.Consolidation, history[0].Author);
    }

    [Fact]
    public async Task The_pending_events_are_those_after_the_last_pass()
    {
        var first = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "one"), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Derived, MemoryEventSource.Agent, "Changed /a.md"), None);
        var second = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Agent, "two"), None);

        Assert.Equal([first.Id, second.Id], (await _memory.PendingAsync(_user, 10, None)).Select(e => e.Id));
        Assert.Equal([first.Id], (await _memory.PendingAsync(_user, 1, None)).Select(e => e.Id));

        await _memory.CloseConsolidationAsync(_user, [first.Id], [], None);
        Assert.Equal([second.Id], (await _memory.PendingAsync(_user, 10, None)).Select(e => e.Id));
        await _memory.CloseConsolidationAsync(_user, [second.Id], [], None);
        Assert.Empty(await _memory.PendingAsync(_user, 10, None));
        Assert.Contains("[event", Consolidation.Prompt([first]));
    }

    private static async Task<string> Call(MemoryTools tools, string name, Dictionary<string, object?> arguments)
    {
        var function = tools.Tools.OfType<AIFunction>().Single(t => t.Name == name);
        var result = await function.InvokeAsync(new AIFunctionArguments(arguments), None);
        return result?.ToString()?.Trim('"') ?? string.Empty;
    }
}
