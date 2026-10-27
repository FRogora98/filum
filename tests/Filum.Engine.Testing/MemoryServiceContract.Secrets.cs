using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Testing;

// Spec 048: a pasted secret is kept nowhere, whatever path it takes.
public abstract partial class MemoryServiceContract
{
    private const string PastedKey = "sk-" + "proj-abcdefghijklmnopqrstuvwxyz123456";

    [Fact]
    public async Task Memory_WhenASecretIsSaidWrittenOrRecorded_ShouldKeepItNowhere()
    {
        var said = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, $"here is my key {PastedKey}"), None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/setup.md", $"Setup notes.\n\nkey: {PastedKey}\n", None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "api key", PastedKey, "2026-01-01", null, [], None));

        var events = await _memory.EventsAsync(_user, new EventQuery(), None);
        var file = Ok(await _memory.ReadAsync(_user, "/notes/setup.md", null, null, None));
        var fact = Assert.Single(Ok(await _memory.CurrentFactsAsync(_user, "the person", None)));

        Assert.DoesNotContain(PastedKey, Assert.Single(events, e => e.Id == said.Id).Text);
        Assert.DoesNotContain(PastedKey, string.Join('\n', file.Lines));
        Assert.DoesNotContain(PastedKey, fact.Value);
    }
}
