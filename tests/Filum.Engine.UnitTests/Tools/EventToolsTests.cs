using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.UnitTests.Tools;

public sealed class EventToolsTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly Guid _user = Guid.NewGuid();
    private readonly IMemoryService _memory = MemoryServices.InMemory();

    [Fact]
    public async Task EventsSearch_WhenNothingInTheWindowMatchesButSomethingOutsideDoes_ShouldSayWhenItWasSaid()
    {
        await Said("I rode a rollercoaster three times in July.", new DateTimeOffset(2023, 11, 4, 3, 27, 0, TimeSpan.Zero));
        await Said("Another rollercoaster in October.", new DateTimeOffset(2023, 11, 6, 9, 0, 0, TimeSpan.Zero));

        var found = await Search(new() { ["query"] = "rollercoaster", ["from"] = "2023-07-01", ["to"] = "2023-10-31" });

        Assert.StartsWith("Nothing that was said between 2023-07-01 and 2023-10-31 matches.", found);
        Assert.Contains("2 matches were said on other days: 2023-11-04, 2023-11-06", found);
        Assert.Contains("the days of what was said, not of what it tells", found);
    }

    [Fact]
    public async Task EventsSearch_WhenNothingMatchesAnywhere_ShouldSayNothingMatches()
    {
        await Said("I rode a rollercoaster three times in July.", new DateTimeOffset(2023, 11, 4, 3, 27, 0, TimeSpan.Zero));

        Assert.Equal("Nothing that was said matches.", await Search(new() { ["query"] = "volcano", ["from"] = "2023-07-01", ["to"] = "2023-10-31" }));
    }

    [Fact]
    public async Task EventsSearch_WhenTheWindowHoldsAMatch_ShouldGiveIt()
    {
        await Said("A rollercoaster in August.", new DateTimeOffset(2023, 8, 20, 10, 0, 0, TimeSpan.Zero));
        await Said("A rollercoaster in November.", new DateTimeOffset(2023, 11, 4, 3, 27, 0, TimeSpan.Zero));

        var found = await Search(new() { ["query"] = "rollercoaster", ["from"] = "2023-07-01", ["to"] = "2023-10-31" });

        Assert.Contains("· 2023-08-20 10:00 UTC · the person:", found);
        Assert.DoesNotContain("2023-11-04", found);
    }

    [Fact]
    public async Task EventsSearch_WhenTheQueryNamesASubjectWithFacts_ShouldGiveItsFactsEvenWithNothingSaid()
    {
        var agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
        await _memory.RecordFactAsync(_user, agent, "Book Seven", "author", "Ada Brook", "2026-01-01", null, [], None);
        await _memory.RecordFactAsync(_user, agent, "Ada Brook", "spouse", "River Okafor", "2026-01-01", null, [], None);

        var found = await Search(new() { ["query"] = "who wrote Book Seven" });

        Assert.Contains("- Book Seven · author: Ada Brook", found);
        Assert.Contains("- Ada Brook · spouse: River Okafor", found);
        Assert.Contains("Nothing that was said matches.", found);
    }

    [Fact]
    public async Task EventsSearch_WhenSomethingMatches_ShouldNameEachMatchByItsEventNumber()
    {
        var said = await Said("A rollercoaster in August.", new DateTimeOffset(2023, 8, 20, 10, 0, 0, TimeSpan.Zero));

        var found = await Search(new() { ["query"] = "rollercoaster" });

        Assert.Contains($"#{said.Id} · 2023-08-20 10:00 UTC · the person:", found);
    }

    [Fact]
    public async Task EventsRead_WhenAnEventOfAConversationIsGiven_ShouldGiveTheWholeConversationInOrderAndNoOther()
    {
        var chat = Guid.NewGuid();
        var day = new DateTimeOffset(2023, 5, 1, 9, 0, 0, TimeSpan.Zero);
        await Said("I am planning a trip to the coast.", day, chat);
        var middle = await Said(new string('x', 900) + " The hotel is the one by the old lighthouse.", day.AddMinutes(1), chat, MemoryEventKind.Answered);
        await Said("Then book it for June.", day.AddMinutes(2), chat);
        await Said("Something else entirely.", day.AddMinutes(3), Guid.NewGuid());

        var read = await Read(new() { ["event"] = middle.Id });

        var trip = read.IndexOf("I am planning a trip to the coast.", StringComparison.Ordinal);
        var hotel = read.IndexOf("The hotel is the one by the old lighthouse.", StringComparison.Ordinal);
        var june = read.IndexOf("Then book it for June.", StringComparison.Ordinal);
        Assert.True(trip >= 0 && hotel > trip && june > hotel, read);
        Assert.Contains($"#{middle.Id} · 2023-05-01 09:01 UTC · you:", read);
        Assert.DoesNotContain("Something else entirely.", read);
    }

    [Fact]
    public async Task EventsRead_WhenTheEventHasNoConversation_ShouldGiveThatDaysMessages()
    {
        var day = new DateTimeOffset(2023, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var first = await SaidOutsideAChat("Morning note.", day);
        await SaidOutsideAChat("Evening note.", day.AddHours(10));
        await SaidOutsideAChat("Next day note.", day.AddDays(1));

        var read = await Read(new() { ["event"] = first.Id });

        Assert.Contains("Morning note.", read);
        Assert.Contains("Evening note.", read);
        Assert.DoesNotContain("Next day note.", read);
    }

    [Fact]
    public async Task EventsRead_WhenTheConversationIsLongerThanTheBudget_ShouldGiveWholeMessagesAndWhereToContinue()
    {
        var chat = Guid.NewGuid();
        var day = new DateTimeOffset(2023, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var events = new List<MemoryEvent>();
        for (var i = 0; i < 6; i++)
        {
            events.Add(await Said($"Message {i} " + new string('y', 5_000), day.AddMinutes(i), chat));
        }

        var first = await Read(new() { ["event"] = events[0].Id });
        var next = events.First(e => !first.Contains($"Message {e.Text[8]} ", StringComparison.Ordinal));
        var rest = await Read(new() { ["event"] = next.Id });

        Assert.Contains($"read them with event {next.Id}", first);
        Assert.DoesNotContain($"Message {next.Text[8]} ", first);
        Assert.Contains($"Message {next.Text[8]} ", rest);
        Assert.Contains("Message 5 ", rest);
        Assert.DoesNotContain("Message 0 ", rest);
    }

    [Fact]
    public async Task EventsRead_WhenAMessageIsPrivate_ShouldLeaveItOutAndRefuseItsNumber()
    {
        var chat = Guid.NewGuid();
        var day = new DateTimeOffset(2023, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var open = await Said("An open message.", day, chat);
        var hidden = await Said("A private message.", day.AddMinutes(1), chat, sensitivity: MemorySensitivity.Private);

        var read = await Read(new() { ["event"] = open.Id });

        Assert.Contains("An open message.", read);
        Assert.DoesNotContain("A private message.", read);
        Assert.Equal($"Refused: There is no event #{hidden.Id}.", await Read(new() { ["event"] = hidden.Id }));
        Assert.Equal("Refused: There is no event #999999.", await Read(new() { ["event"] = 999_999 }));
    }

    private Task<MemoryEvent> Said(string text, DateTimeOffset when, Guid? conversation = null, string kind = MemoryEventKind.Said, string sensitivity = MemorySensitivity.Normal) =>
        _memory.RecordAsync(_user, new NewMemoryEvent(kind, MemoryEventSource.Chat, text, when, conversation ?? Guid.NewGuid(), Sensitivity: sensitivity), None);

    private Task<MemoryEvent> SaidOutsideAChat(string text, DateTimeOffset when) =>
        _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, text, when), None);

    private Task<string> Search(Dictionary<string, object?> arguments) => Tool("events_search", arguments);

    private Task<string> Read(Dictionary<string, object?> arguments) => Tool("events_read", arguments);

    private Task<string> Tool(string name, Dictionary<string, object?> arguments) =>
        Call(new MemoryTools(_memory, new MemoryOptions(), new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid())), name, arguments);
}
