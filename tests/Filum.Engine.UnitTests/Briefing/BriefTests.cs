using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;

namespace Filum.Engine.UnitTests.Briefing;

// Spec 045: what an agent knows before the first message, and about each message.
public sealed class BriefTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task BriefAsync_WhenTheMemoryHoldsEverything_ShouldGiveTheFiveSectionsInOrderWithinTheBudget()
    {
        await _memory.WriteAsync(_user, Agent, "/filum.md", "# The person\n\nA person who likes short answers.", None);
        await _memory.WriteAsync(_user, Agent, "/learned.md", "What was learned.\n- 2026-10-01 calls the dog Rex\n- 2026-10-10 prefers mornings", None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "city", "Lyon", "2026-01-01", null, [], None);
        await _memory.WriteAsync(_user, Agent, "/notes/garden.md", "Notes about the garden.", None);
        await Said("I planted tomatoes.");

        var brief = await _memory.BriefAsync(_user, null, None);

        var order = new[] { "A person who likes short answers.", "- the person · city: Lyon", "prefers mornings", "/notes/garden.md", "1 messages kept" }
            .Select(s => brief.IndexOf(s, StringComparison.Ordinal)).ToList();
        Assert.All(order, i => Assert.True(i >= 0, brief));
        Assert.Equal(order.Order(), order);
        Assert.True(brief.IndexOf("prefers mornings", StringComparison.Ordinal) < brief.IndexOf("calls the dog Rex", StringComparison.Ordinal), "learned lines come newest first");
        Assert.True(brief.Length <= new MemoryOptions().BriefChars);
    }

    [Fact]
    public async Task BriefAsync_WhenMoreFactsHoldThanFit_ShouldShowTheMostRecentAndSayHowManyMore()
    {
        for (var i = 0; i < 300; i++)
        {
            await _memory.RecordFactAsync(_user, Agent, $"subject {i:000}", "attribute", $"value {i}", "2026-01-01", null, [], None);
        }

        var brief = await _memory.BriefAsync(_user, 3_000, None);

        Assert.Contains("subject 299", brief);
        Assert.DoesNotContain("subject 000 ", brief);
        Assert.Matches(@"\(\+\d+ more lines\)", brief);
        Assert.True(brief.Length <= 3_000, $"{brief.Length} characters");
    }

    [Fact]
    public async Task BriefAndRecall_WhenFilesAndMessagesArePrivate_ShouldShowNeitherOfThem()
    {
        await _memory.WriteAsync(_user, Agent, "/notes/health.md", "A private note about the clinic.", None);
        await _memory.SetSensitivityAsync(_user, Agent, "/notes/health.md", MemorySensitivity.Private, None);
        await Said("The clinic appointment is secret.", MemorySensitivity.Private);

        var brief = await _memory.BriefAsync(_user, null, None);
        var recall = await _memory.RecallAsync(_user, "when is the clinic appointment", null, None);

        Assert.DoesNotContain("clinic", brief);
        Assert.DoesNotContain("clinic", recall);
    }

    [Fact]
    public async Task RecallAsync_WhenAFactAndAMessageMatch_ShouldShowBothAndNothingForAnUnrelatedMessage()
    {
        var said = await Said("We moved the piano to the attic last week.");
        await _memory.RecordFactAsync(_user, Agent, "the piano", "room", "attic", "2026-10-01", null, [said.Id], None);

        var recall = await _memory.RecallAsync(_user, "where is the piano now?", null, None);

        Assert.Contains("- the piano · room: attic", recall);
        Assert.Contains($"#{said.Id}", recall);
        Assert.True(recall.Length <= new MemoryOptions().RecallChars);
        Assert.Equal(string.Empty, await _memory.RecallAsync(_user, "quantum chromodynamics", null, None));
    }

    [Fact]
    public async Task BriefAndRecall_WhenTheMemoryHoldsARule_ShouldPutItFirstAndRepeatItWithEveryMessage()
    {
        await _memory.WriteAsync(_user, Agent, "/filum.md", "# The person\n\nA person.", None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "rule", "answers in short bullet points", "2026-01-01", null, [], None);
        await _memory.RecordFactAsync(_user, Agent, "the piano", "room", "attic", "2026-10-01", null, [], None);

        var brief = await _memory.BriefAsync(_user, null, None);
        var recall = await _memory.RecallAsync(_user, "where is the piano now?", null, None);
        var unrelated = await _memory.RecallAsync(_user, "quantum chromodynamics", null, None);

        Assert.True(brief.IndexOf("answers in short bullet points", StringComparison.Ordinal) < brief.IndexOf("A person.", StringComparison.Ordinal), brief);
        Assert.StartsWith("What the person's memory (Filum) holds about them.", brief);
        Assert.True(recall.IndexOf("answers in short bullet points", StringComparison.Ordinal) < recall.IndexOf("the piano · room: attic", StringComparison.Ordinal), recall);
        Assert.Equal(3, recall.Split("answers in short bullet points").Length);
        Assert.EndsWith("Before you answer, follow: answers in short bullet points.", recall);
        Assert.EndsWith("Before you answer, follow: answers in short bullet points.", unrelated);
        Assert.True(recall.Length <= new MemoryOptions().RecallChars);
    }

    [Fact]
    public async Task RulesAsync_WhenRulesHoldAndWhenTheFactsArePrivate_ShouldGiveThemNewestFirstWithinTheLimitOrNothing()
    {
        await _memory.RecordFactAsync(_user, Agent, "the person", "rule", "answers in short bullet points", "2026-01-01", null, [], None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "rule", "temperatures in Celsius", "2026-02-01", null, [], None);
        await _memory.RecordFactAsync(_user, Agent, "the piano", "room", "attic", "2026-10-01", null, [], None);

        var rules = await _memory.RulesAsync(_user, null, None);
        var cut = await _memory.RulesAsync(_user, 90, None);
        await _memory.SetSensitivityAsync(_user, Agent, "/facts.csv", MemorySensitivity.Private, None);

        Assert.StartsWith("# Rules the person set", rules);
        Assert.True(rules.IndexOf("temperatures in Celsius", StringComparison.Ordinal) < rules.IndexOf("answers in short bullet points", StringComparison.Ordinal), rules);
        Assert.DoesNotContain("attic", rules);
        Assert.True(cut.Length <= 90, cut);
        Assert.Equal(string.Empty, await _memory.RulesAsync(_user, null, None));
        Assert.Equal(string.Empty, await MemoryServices.InMemory().RulesAsync(_user, null, None));
    }

    [Fact]
    public async Task BriefRecallAndCurrent_WhenAValueStartsLater_ShouldShowTheOneItReplacesAsHoldingNowAndItAsPlanned()
    {
        var later = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await _memory.RecordFactAsync(_user, Agent, "the person", "city", "Utrecht", "2026-01-01", null, [], None);
        await _memory.RecordFactAsync(_user, Agent, "the person", "city", "Rotterdam", later, null, [], None);

        var current = (await _memory.CurrentFactsAsync(_user, null, None)).Value!;
        var planned = (await _memory.PlannedFactsAsync(_user, null, None)).Value!;
        var brief = await _memory.BriefAsync(_user, null, None);
        var recall = await _memory.RecallAsync(_user, "which city do I live in?", null, None);

        Assert.Equal(["Utrecht"], current.Select(f => f.Value));
        Assert.Equal(["Rotterdam"], planned.Select(f => f.Value));
        Assert.Contains("- the person · city: Utrecht (since 2026-01-01)", brief);
        Assert.Contains($"- from {later}: the person · city: Rotterdam", brief);
        Assert.Contains($"- from {later}: the person · city: Rotterdam", recall);
    }

    [Fact]
    public async Task EndFactAsync_WhenTheEventsThatEndedItAreGiven_ShouldKeepThemWithTheOnesItCameFrom()
    {
        var promised = await Said("I promised to call the plumber.");
        var done = await Said("Called the plumber this morning, it's sorted.");
        await _memory.RecordFactAsync(_user, Agent, "the person", "open", "call the plumber", "2026-01-01", null, [promised.Id], None);

        await _memory.EndFactAsync(_user, Agent, "the person", "open", "2026-01-02", false, "call the plumber", [done.Id], None);

        var ended = (await _memory.FactHistoryAsync(_user, "the person", "open", None)).Value!.Single();
        Assert.Equal([promised.Id, done.Id], ended.Sources);
        Assert.Equal("2026-01-02", ended.ValidTo);
    }

    private Task<MemoryEvent> Said(string text, string sensitivity = MemorySensitivity.Normal) =>
        _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, text, Sensitivity: sensitivity), None);
}
