using Filum.Agent.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>The turn and the person's log of events (spec 030), through the sample host.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EventLogTests(PostgresFixture postgres)
{
    private static Dictionary<string, object?> Edit(string oldText, string newText) =>
        new() { ["path"] = "/filum.md", ["oldText"] = oldText, ["newText"] = newText };

    [Fact]
    public async Task What_the_person_said_is_recorded_before_the_model_runs_and_the_answer_and_the_changes_after_it()
    {
        var llm = new FakeChatClient();
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        var person = host.Person(id);
        var saidBeforeTheModel = 0;
        llm.Reply = _ =>
        {
            saidBeforeTheModel = Events(host, id).Count(e => e.Kind == MemoryEventKind.Said);
            return "Saved your plan.";
        };
        llm.Then(Call("memory_write", new Dictionary<string, object?> { ["path"] = "/notes/plan.md", ["content"] = "Paint the fence\n" }));
        var conversation = Guid.NewGuid();

        await Send(person, conversation, "Remember that I will paint the fence on Saturday.");

        Assert.Equal(1, saidBeforeTheModel);
        var events = Events(host, id);
        Assert.Equal([MemoryEventKind.Said, MemoryEventKind.Answered, MemoryEventKind.Derived], events.Select(e => e.Kind));
        var said = events[0];
        Assert.Equal(("Remember that I will paint the fence on Saturday.", MemoryEventSource.Chat, (Guid?)conversation), (said.Text, said.Source, said.ConversationId));
        Assert.Equal("Saved your plan.", events[1].Text);
        Assert.Equal([said.Id], events[2].Sources);
        Assert.Single(events[2].Revisions);
        Assert.Contains("/notes/plan.md", events[2].Text);
    }

    [Fact]
    public async Task A_name_given_in_one_message_is_known_at_the_next_one_and_in_a_new_chat_with_no_consolidation()
    {
        var llm = new FakeChatClient { Reply = _ => "Nice to meet you, Sam." };
        await using var host = new SampleHostFactory(postgres, llm);
        var person = host.Person(Guid.NewGuid());
        var chat = Guid.NewGuid();
        llm.Then(Call("memory_edit", Edit("# About you\n", "# About you\nName: Sam\n")));

        await Send(person, chat, "My name is Sam.");
        await Send(person, chat, "What is my name?");
        var sameChat = llm.Calls[^1];
        await Send(person, Guid.NewGuid(), "Do you know my name?");

        Assert.Contains(sameChat, m => m.Text == "My name is Sam.");
        Assert.Contains("Name: Sam", llm.InstructionsOf(llm.Calls.Count - 1));
    }

    [Fact]
    public async Task A_rule_written_into_the_core_records_what_the_person_told_with_their_message_as_its_source()
    {
        var llm = new FakeChatClient { Reply = _ => "From now on I will be brief." };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        var person = host.Person(id);
        llm.Then(Call("memory_edit", Edit("# Rules\n", "# Rules\n- Answer briefly.\n")));

        await Send(person, Guid.NewGuid(), "From now on, answer briefly.");
        llm.Then(Call("memory_edit", Edit("# About you\n", "# About you\nLives by the sea.\n")));
        await Send(person, Guid.NewGuid(), "I live by the sea.");

        var events = Events(host, id);
        var told = Assert.Single(events, e => e.Kind == MemoryEventKind.Told);
        var said = events.First(e => e.Kind == MemoryEventKind.Said);
        Assert.Equal("From now on, answer briefly.", told.Text);
        Assert.Equal([said.Id], told.Sources);
        Assert.Contains("- Answer briefly.", PlatformInstructions.RulesOf(await Core(host, id)));
    }

    [Fact]
    public async Task A_failed_turn_keeps_what_was_said_and_records_no_answer_and_no_change()
    {
        var llm = new FakeChatClient { FailAfterToolCalls = true };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        llm.Then(Call("memory_write", new Dictionary<string, object?> { ["path"] = "/notes/a.md", ["content"] = "a\n" }));

        var response = await host.Person(id).Send(Guid.NewGuid(), "Write a note.");

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal([MemoryEventKind.Said], Events(host, id).Select(e => e.Kind));
    }

    private static IReadOnlyList<MemoryEvent> Events(SampleHostFactory host, Guid person)
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MemoryService>().EventsAsync(person, new EventQuery(), CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task<string> Core(SampleHostFactory host, Guid person)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MemoryService>().EnsureCoreAsync(person, CancellationToken.None);
    }

    private static async Task Send(HttpClient person, Guid conversation, string content)
    {
        var response = await person.Send(conversation, content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
