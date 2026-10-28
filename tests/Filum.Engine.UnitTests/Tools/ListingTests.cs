using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Collections.Compute.Jint;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using static Filum.Engine.UnitTests.Builders.ToolCalls;

namespace Filum.Engine.UnitTests.Tools;

// Spec 057: events_search with every lists each mention to count it.
public sealed class ListingTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DateTimeOffset Day = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _user = Guid.NewGuid();
    private readonly MemoryActor _agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
    private readonly IMemoryService _memory = MemoryServices.InMemory();
    private MemoryOptions _options = new();

    [Fact]
    public async Task EventsSearch_WhenEveryIsAsked_ShouldListEachMentionInDayOrderOnALineAndSayItIsAll()
    {
        var first = await Said("I assembled the walnut bookshelf in the study.", Day.AddDays(3));
        await Said("Nothing about furniture here, just the weather.", Day.AddDays(4));
        var second = await Said($"{new string('x', 400)} then I sold the old sofa for 80 dollars {new string('y', 400)}", Day.AddDays(1));

        var listed = await Search("bookshelf | sofa");

        Assert.StartsWith("Every message said that mentions \"bookshelf | sofa\", oldest first:", listed);
        Assert.True(listed.IndexOf($"#{second.Id}", StringComparison.Ordinal) < listed.IndexOf($"#{first.Id}", StringComparison.Ordinal), listed);
        Assert.Contains("2026-01-02", listed);
        Assert.Contains("sold the old sofa for 80 dollars", listed);
        Assert.DoesNotContain("weather", listed);
        Assert.Contains("2 messages listed: all that match.", listed);
    }

    [Fact]
    public async Task EventsSearch_WhenAListingReachesItsCaps_ShouldSayThereMayBeMore()
    {
        _options = new MemoryOptions { EventsListMax = 3 };
        for (var i = 0; i < 6; i++)
        {
            await Said($"Another lamp, number {i}.", Day.AddDays(i));
        }

        var listed = await Search("lamp");

        Assert.Equal(3, listed.Split('\n').Count(l => l.StartsWith("- ", StringComparison.Ordinal)));
        Assert.Contains("3 messages listed; there may be more: narrow the words or the days.", listed);
    }

    [Fact]
    public async Task EventsSearch_WhenANormalSearchIsCut_ShouldSayToListAndNotWhenItIsNot()
    {
        _options = new MemoryOptions { EventsSearchMax = 8 };
        for (var i = 0; i < 10; i++)
        {
            await Said($"Another lamp, number {i}.", Day.AddDays(i));
        }

        await Said("The zebra crossing was repainted.", Day);
        Assert.Contains("list them with every", await Call(Tools(), "events_search", new() { ["query"] = "lamp" }));
        Assert.DoesNotContain("list them with every", await Call(Tools(), "events_search", new() { ["query"] = "zebra" }));
    }

    private Task<MemoryEvent> Said(string text, DateTimeOffset when) =>
        _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, text, when, Guid.NewGuid()), None);

    private MemoryTools Tools() => new(_memory, _options, new JintCodeSandbox(), _user, _agent);

    private Task<string> Search(string query) => Call(Tools(), "events_search", new() { ["query"] = query, ["every"] = true });
}
