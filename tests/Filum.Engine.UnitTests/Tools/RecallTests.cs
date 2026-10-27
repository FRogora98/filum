using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Search;
using Filum.Engine.Collections.Compute.Jint;
using Filum.Engine.Memory;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using Filum.Engine.UnitTests.Fakes;
using static Filum.Engine.UnitTests.Builders.ToolCalls;

namespace Filum.Engine.UnitTests.Tools;

// Spec 042: what events_search shows.
public sealed class RecallTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DateTimeOffset Day = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _user = Guid.NewGuid();
    private readonly MemoryActor _agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
    private IMemoryService _memory = MemoryServices.InMemory();
    private MemoryOptions _options = new();

    [Fact]
    public async Task EventsSearch_WhenAFactMatchesTheWordsButNotItsSubjectsName_ShouldShowItWithItsSource()
    {
        var said = await Said("We talked for an hour about moving.", Day, Guid.NewGuid());
        await _memory.RecordFactAsync(_user, _agent, "Anna", "city", "Lyon", "2026-03-01", null, [said.Id], None);

        var found = await Search("which city did she move to");

        Assert.Contains("Facts that may answer it", found);
        Assert.Contains($"- Anna · city: Lyon (since 2026-03-01 by the agent; from event {said.Id})", found);
    }

    [Fact]
    public async Task EventsSearch_WhenAFactMatchesOnlyByMeaning_ShouldShowItAndNotAnUnrelatedOne()
    {
        _memory = MemoryServices.On(new InMemoryMemoryStore(), embedder: new FamilyEmbedder());
        await _memory.RecordFactAsync(_user, _agent, "the person", "ferns", "3", "2026-03-01", null, [], None);
        await _memory.RecordFactAsync(_user, _agent, "the person", "commute", "train", "2026-03-01", null, [], None);

        var found = await Search("what grows on my balcony");

        Assert.Contains("- the person · ferns: 3", found);
        Assert.DoesNotContain("commute", found);
    }

    [Fact]
    public async Task EventsSearch_WhenAFactMatches_ShouldShowTheConversationItCameFromFirst()
    {
        var chat = Guid.NewGuid();
        await Said("Good morning, how are you?", Day, chat);
        var source = await Said("She has settled in her new flat near the river.", Day.AddMinutes(1), chat);
        await Said("Anna sent a postcard from her holiday.", Day.AddDays(3), Guid.NewGuid());
        await _memory.RecordFactAsync(_user, _agent, "Anna", "city", "Lyon", "2026-03-01", null, [source.Id], None);

        var found = await Search("Anna city");

        var conversation = found.IndexOf("where Anna · city was said", StringComparison.Ordinal);
        Assert.True(conversation >= 0, found);
        Assert.True(found.IndexOf("She has settled in her new flat", StringComparison.Ordinal) > conversation);
        Assert.True(found.IndexOf("Anna sent a postcard", StringComparison.Ordinal) > found.IndexOf("She has settled", StringComparison.Ordinal));
        Assert.Contains("Good morning, how are you?", found);
    }

    [Fact]
    public async Task EventsSearch_WhenThreeConversationsMatchFarBetterThanTheRest_ShouldShowOnlyThose()
    {
        for (var i = 0; i < 3; i++)
        {
            await Said($"Strong {i}: the volcano eruption covered the valley in lava.", Day.AddDays(i), Guid.NewGuid());
        }

        for (var i = 0; i < 3; i++)
        {
            await Said($"Weak {i}: a lamp shaped like lava.", Day.AddDays(10 + i), Guid.NewGuid());
        }

        var found = await Search("volcano eruption lava");

        Assert.All(Enumerable.Range(0, 3), i => Assert.Contains($"Strong {i}", found));
        Assert.All(Enumerable.Range(0, 3), i => Assert.DoesNotContain($"Weak {i}", found));
    }

    [Fact]
    public async Task EventsSearch_WhenManyConversationsMatchAlike_ShouldShowNoMoreThanTheLimit()
    {
        for (var i = 0; i < 7; i++)
        {
            await Said($"A note about the garden, number {i}.", Day.AddDays(i), Guid.NewGuid());
        }

        var found = await Search("garden");

        Assert.Equal(_options.RecallSessionsMax, found.Split("=== ").Length - 1);
    }

    [Fact]
    public async Task EventsSearch_WhenAConversationIsShort_ShouldShowItWholeAndALongOneByItsMatches()
    {
        var shortChat = Guid.NewGuid();
        await Said("I am planning a trip.", Day, shortChat);
        await Said("The lighthouse hotel is booked.", Day.AddMinutes(1), shortChat, MemoryEventKind.Answered);
        await Said("Thank you.", Day.AddMinutes(2), shortChat);
        var longChat = Guid.NewGuid();
        for (var i = 0; i < 20; i++)
        {
            await Said($"Filler {i} " + new string('z', 2_000), Day.AddDays(1).AddMinutes(i), longChat);
        }

        await Said("The lighthouse tour starts at noon.", Day.AddDays(1).AddMinutes(30), longChat);
        await Said("After it, lunch by the harbour.", Day.AddDays(1).AddMinutes(31), longChat, MemoryEventKind.Answered);

        var found = await Search("lighthouse");

        Assert.Contains("I am planning a trip.", found);
        Assert.Contains("Thank you.", found);
        Assert.Contains("The lighthouse tour starts at noon.", found);
        Assert.Contains("After it, lunch by the harbour.", found);
        Assert.DoesNotContain("Filler 0 ", found);
        Assert.Contains("22 messages in all: read the conversation with events_read", found);
    }

    [Fact]
    public async Task EventsSearch_WhenMuchMatches_ShouldStayWithinTheBudget()
    {
        _options = new MemoryOptions { RecallBudgetChars = 3_000 };
        for (var i = 0; i < 8; i++)
        {
            await Said($"The archive {i} " + new string('a', 4_000), Day.AddDays(i), Guid.NewGuid());
        }

        var found = await Search("archive");

        // The budget is for what was said; the lines that name conversations and events come on top.
        Assert.True(found.Length < 3_000 + 1_000, $"{found.Length} characters");
    }

    [Fact]
    public async Task EventsSearch_WhenARerankerOrdersAgain_ShouldShowItsOrder()
    {
        _memory = MemoryServices.On(new InMemoryMemoryStore(), reranker: new LastFirstReranker());
        await Said("The museum of the sea: the museum.", Day, Guid.NewGuid());
        await Said("A leaflet from a shop in town, with a list of prices and a museum named at the end of it.", Day.AddDays(1), Guid.NewGuid());

        var found = await Search("museum");

        Assert.True(found.IndexOf("A leaflet from a shop", StringComparison.Ordinal) < found.IndexOf("The museum of the sea", StringComparison.Ordinal), found);
    }

    [Fact]
    public async Task EventsSearch_WhenTheRerankerFails_ShouldAnswerInItsOwnOrder()
    {
        _memory = MemoryServices.On(new InMemoryMemoryStore(), reranker: new FailingReranker());
        await Said("The museum of the sea: the museum.", Day, Guid.NewGuid());
        await Said("A leaflet from a shop in town, with a list of prices and a museum named at the end of it.", Day.AddDays(1), Guid.NewGuid());

        var found = await Search("museum");

        Assert.True(found.IndexOf("The museum of the sea", StringComparison.Ordinal) < found.IndexOf("A leaflet from a shop", StringComparison.Ordinal), found);
    }

    [Theory]
    [InlineData(new[] { 9.0, 8.5, 8.0, 1.0, 0.9 }, 5, 3)]
    [InlineData(new[] { 9.0, 1.0 }, 5, 1)]
    [InlineData(new[] { 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0 }, 5, 5)]
    [InlineData(new[] { 3.0, 3.0 }, 5, 2)]
    public void Count_WhenScoresDrop_ShouldStopAtTheLargestDrop(double[] scores, int max, int expected) =>
        Assert.Equal(expected, RecallPage.Count(scores, max));

    private Task<MemoryEvent> Said(string text, DateTimeOffset when, Guid conversation, string kind = MemoryEventKind.Said) =>
        _memory.RecordAsync(_user, new NewMemoryEvent(kind, MemoryEventSource.Chat, text, when, conversation), None);

    private Task<string> Search(string query) =>
        Call(new MemoryTools(_memory, _options, new JintCodeSandbox(), _user, _agent), "events_search", new() { ["query"] = query });

    // The order reversed: the last match scores highest.
    private sealed class LastFirstReranker : IReranker
    {
        public IReadOnlyList<float>? Score(string query, IReadOnlyList<string> texts) => [.. texts.Select((_, i) => (float)(i + 1))];
    }

    private sealed class FailingReranker : IReranker
    {
        public IReadOnlyList<float>? Score(string query, IReadOnlyList<string> texts) => throw new InvalidOperationException("no model");
    }
}
