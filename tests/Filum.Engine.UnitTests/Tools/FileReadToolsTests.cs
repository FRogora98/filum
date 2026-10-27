using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Instructions;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.Testing.Builders.Outcomes;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class FileReadToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task MemoryOverview_WhenCalled_ShouldGiveTheCoreTheIndexAndTheEnabledPublicSkills()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/plans.md", "one\ntwo\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/secret.md", "hidden\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/secret.md", MemorySensitivity.Private, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("log-note", "Keep a note", "the person says note that", true, "1. Add it."), false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("old-way", "An old way", "never", true, "1. Nothing."), false, None));
        Ok(await _memory.SetSkillEnabledAsync(_user, Agent, "old-way", false, None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, Agent);

        var overview = await Call(tools, "memory_overview", []);

        Assert.Contains(PlatformInstructions.CoreTemplate, overview);
        Assert.Contains(await _memory.BuildIndexAsync(_user, None), overview);
        Assert.Contains("- /notes/plans.md (document · 2 lines", overview);
        Assert.DoesNotContain("secret", overview);
        Assert.Contains("- /log-note: Keep a note · when the person says note that", overview);
        Assert.DoesNotContain("old-way", overview);
        Assert.Equal((ToolStep.Read, "Looked at the memory"), (tools.Steps[0].Kind, tools.Steps[0].Description));
    }

    [Fact]
    public async Task MemoryOverview_WhenWhatWasSaidIsKept_ShouldCountItWithItsDatesAndPointToTheSearch()
    {
        var conversation = Guid.NewGuid();
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "I watched 10 episodes.", new DateTimeOffset(2023, 4, 28, 18, 6, 0, TimeSpan.Zero), conversation), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Answered, MemoryEventSource.Chat, "Great progress!", new DateTimeOffset(2023, 4, 28, 18, 7, 0, TimeSpan.Zero), conversation), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "Now 50 episodes.", new DateTimeOffset(2023, 5, 29, 9, 0, 0, TimeSpan.Zero), Guid.NewGuid()), None);

        var overview = await CallAsAgent("memory_overview", []);

        Assert.Contains("# What was said\n\n3 messages kept, from 2023-04-28 to 2023-05-29", overview);
        Assert.Contains("events_search", overview);
    }

    [Fact]
    public async Task MemoryOverview_WhenNothingWasSaid_ShouldGiveTheBriefWithoutThatSection()
    {
        var overview = await CallAsAgent("memory_overview", []);

        Assert.Contains("# The person's core (/filum.md)", overview);
        Assert.DoesNotContain("# What was said", overview);
    }

    [Fact]
    public async Task MemoryOverview_WhenMuchSaidIsNotTidied_ShouldShowTheNewestOfItBounded()
    {
        for (var i = 0; i < 12; i++)
        {
            await _memory.RecordAsync(_user, new Filum.Engine.Abstractions.Events.NewMemoryEvent(Filum.Engine.Abstractions.Events.MemoryEventKind.Said, Filum.Engine.Abstractions.Events.MemoryEventSource.Chat, $"Message {i} " + new string('x', 400)), None);
        }

        var overview = await CallAsAgent("memory_overview", []);

        Assert.Contains("# Said and not tidied yet (12, newest first)", overview);
        Assert.Contains("Message 11", overview);
        Assert.DoesNotContain("Message 0 ", overview);
    }

    [Fact]
    public async Task MemoryOverview_WhenFactsHold_ShouldGiveThemWithTheRulesAndOpenItemsFirst()
    {
        await _memory.RecordFactAsync(_user, Agent, "the person", "city", "Lyon", "2026-01-01", null, [], None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "rule", "answer in short bullet points", "2026-01-01", "they read on the phone", [], None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "open", "send the photos", "2026-01-01", "Friday", [], None);

        var overview = await CallAsAgent("memory_overview", []);

        var rule = overview.IndexOf("- answer in short bullet points (because they read on the phone)", StringComparison.Ordinal);
        var open = overview.IndexOf("- send the photos (due Friday), since 2026-01-01", StringComparison.Ordinal);
        var fact = overview.IndexOf("- the person · city: Lyon", StringComparison.Ordinal);
        Assert.True(rule >= 0 && open > rule && fact > open, overview);
        Assert.DoesNotContain("the person · rule", overview);
    }

    [Fact]
    public async Task MemorySearch_WhenALineHoldsEveryWordInAnotherOrderAndForm_ShouldFindIt()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/people.md", "# People\n\nRiver Okafor: born in Lisbon, two cities later.\n", None));

        var found = await CallAsAgent("memory_search", new() { ["query"] = "Okafor River born city" });

        Assert.Contains("/people.md:3: River Okafor: born in Lisbon", found);
    }

    [Fact]
    public async Task MemorySearch_WhenOneLineHoldsTheTextAsWrittenAndAnotherHoldsItsWords_ShouldListTheLiteralOneFirst()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/a.md", "# A\n\npie with apple and cream\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/b.md", "# B\n\nthe apple pie of Sunday\n", None));

        var found = await CallAsAgent("memory_search", new() { ["query"] = "apple pie" });

        Assert.True(found.IndexOf("/b.md:3", StringComparison.Ordinal) < found.IndexOf("/a.md:3", StringComparison.Ordinal), found);
        Assert.Contains("/a.md:3", found);
    }

    [Fact]
    public async Task MemorySearch_WhenAWordOfTheQueryIsMissingFromTheLine_ShouldNotGiveIt()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/a.md", "# A\n\nRiver Okafor lives in Lisbon\n", None));

        Assert.DoesNotContain("/a.md:3", await CallAsAgent("memory_search", new() { ["query"] = "River Okafor Porto" }));
    }

    [Fact]
    public async Task MemorySearch_WhenTheQueryNamesASubjectWithFacts_ShouldGiveItsCurrentFactsAndPointToTheirHistory()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "River Okafor", "birth city", "Porto", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "River Okafor", "birth city", "Lisbon", "2026-02-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Ada Brook", "birth city", "Turin", "2026-01-01", null, [], None));

        var found = await CallAsAgent("memory_search", new() { ["query"] = "where was river okafor born" });

        Assert.Contains("Facts that hold now about River Okafor", found);
        Assert.Contains("- River Okafor · birth city: Lisbon (since 2026-02-01)", found);
        Assert.DoesNotContain("Porto", found);
        Assert.DoesNotContain("Ada Brook", found);
        Assert.Contains("facts_history", found);
    }

    [Fact]
    public async Task MemorySearch_WhenAFactOfTheNamedSubjectPointsToAnotherSubject_ShouldGiveItsFactsToo()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Book Seven", "author", "Ada Brook", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Ada Brook", "spouse", "River Okafor", "2026-01-01", null, [], None));

        var found = await CallAsAgent("memory_search", new() { ["query"] = "spouse of the author of Book Seven" });

        Assert.Contains("- Book Seven · author: Ada Brook", found);
        Assert.Contains("Facts that hold now about Ada Brook (the value of Book Seven · author):", found);
        Assert.Contains("- Ada Brook · spouse: River Okafor", found);
    }

    [Fact]
    public async Task MemoryRead_WhenALineWasOverwrittenInPlace_ShouldShowWhatItSaidBefore()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/travel.md", "# Travel\n\nFrequent flyer: Premier Silver status with United.\nHome airport: IAD.\n", None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/travel.md", "# Travel\n\nFrequent flyer: Premier Gold status with United.\nHome airport: IAD.\n", None);

        var read = await CallAsAgent("memory_read", new() { ["path"] = "/travel.md" });

        var line = read.Split('\n').Single(l => l.Contains("Premier Gold"));
        Assert.Contains("before: \"…Premier Silver status…\"", line);
        Assert.Contains($"changed {DateTimeOffset.UtcNow:yyyy-MM-dd}", line);
        Assert.DoesNotContain("before", read.Split('\n').Single(l => l.Contains("IAD")));
    }

    [Fact]
    public async Task MemoryRead_WhenACollectionRowWasUpdated_ShouldCarryItsOldValue()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/pantry.csv", "item,dozens\neggs,30\nflour,2\n", None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/pantry.csv", "item,dozens\neggs,20\nflour,2\n", None);

        var read = await CallAsAgent("memory_read", new() { ["path"] = "/pantry.csv" });

        Assert.Contains("eggs,20   (changed", read);
        Assert.Contains("before: \"eggs,30\"", read);
    }

    [Fact]
    public async Task MemoryRead_WhenTheFileNeverChanged_ShouldReadAsBefore()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/notes.md", "One line.\nAnother line.\n", None);

        Assert.DoesNotContain("before:", await CallAsAgent("memory_read", new() { ["path"] = "/notes.md" }));
    }

    [Fact]
    public async Task MemorySearch_WhenFilesAndMessagesMatch_ShouldShowWhatWasSaidNewestFirstAndPrivateMessagesOnlyWhenAsked()
    {
        await _memory.WriteAsync(_user, MemoryActor.Person, "/pantry.md", "We keep 30 dozen eggs in the fridge.\n", None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "We have 30 dozen eggs now.", new DateTimeOffset(2023, 1, 11, 9, 0, 0, TimeSpan.Zero)), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "Down to 20 dozen eggs after the bake sale.", new DateTimeOffset(2023, 3, 2, 9, 0, 0, TimeSpan.Zero)), None);
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "A secret about the eggs.", new DateTimeOffset(2023, 4, 1, 9, 0, 0, TimeSpan.Zero), Sensitivity: MemorySensitivity.Private), None);

        var found = await CallAsAgent("memory_search", new() { ["query"] = "eggs" });

        Assert.Contains("/pantry.md:1: We keep 30 dozen eggs in the fridge.", found);
        var said = found[found.IndexOf("What was said", StringComparison.Ordinal)..];
        Assert.True(said.IndexOf("2023-03-02", StringComparison.Ordinal) < said.IndexOf("2023-01-11", StringComparison.Ordinal), said);
        Assert.DoesNotContain("secret", found);
        Assert.Contains("secret", await CallAsAgent("memory_search", new() { ["query"] = "eggs", ["includePrivate"] = true }));
    }

    [Fact]
    public async Task MemoryRead_WhenAPassAsksForPrivateEventsOrFiles_ShouldNeverGiveThem()
    {
        await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "the secret code is blue", Sensitivity: MemorySensitivity.Private), None);
        await _memory.WriteAsync(_user, MemoryActor.Person, "/notes/secret.md", "# Secret\nthe code is blue\n", None);
        await _memory.SetSensitivityAsync(_user, MemoryActor.Person, "/notes/secret.md", MemorySensitivity.Private, None);
        var pass = Pass();

        var said = await Call(pass, "events_search", new() { ["query"] = "secret code", ["includePrivate"] = true });
        var lines = await Call(pass, "memory_search", new() { ["query"] = "code", ["includePrivate"] = true });
        var read = await Call(pass, "memory_read", new() { ["path"] = "/notes/secret.md" });

        Assert.DoesNotContain("blue", said);
        Assert.DoesNotContain("blue", lines);
        Assert.Contains("is private: a pass does not read it", read);
    }

    private MemoryTools Pass() => new(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Consolidation, consolidation: true);

    private Task<string> CallAsAgent(string name, Dictionary<string, object?> arguments) =>
        Call(new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid())), name, arguments);
}
