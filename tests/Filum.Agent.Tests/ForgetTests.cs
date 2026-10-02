using Microsoft.Extensions.AI;
using Filum.Agent.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>Deleting and forgetting a conversation (spec 030), through the sample host.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ForgetTests(PostgresFixture postgres)
{
    private static FunctionCallContent Write(string path, string content) =>
        Call("memory_write", new Dictionary<string, object?> { ["path"] = path, ["content"] = content });

    private static FunctionCallContent Fact(string attribute, string value) =>
        Call("fact_record", new Dictionary<string, object?> { ["subject"] = "the person", ["attribute"] = attribute, ["value"] = value });

    [Fact]
    public async Task Forgetting_a_conversation_removes_what_was_said_and_what_came_only_from_it_and_keeps_the_rest()
    {
        var llm = new FakeChatClient { Reply = _ => "Done." };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        var person = host.Person(id);
        var kept = Guid.NewGuid();
        var forgotten = Guid.NewGuid();
        llm.Then(Write("/notes/shared.md", "# Shared\n"), Fact("city", "Lisbon"));
        await Send(person, kept, "Start a shared note; I live in Lisbon.");
        llm.Then(Write("/notes/secret.md", "# Secret\nThe surprise party is on Friday.\n"), Fact("party", "Friday"));
        await Send(person, forgotten, "Keep the surprise party on Friday in mind.");
        llm.Then(Call("memory_append", new Dictionary<string, object?> { ["path"] = "/notes/shared.md", ["text"] = "A line from the forgotten chat" }));
        await Send(person, forgotten, "Add a line to the shared note.");

        var response = await person.PostAsJsonAsync("/sample/memory/forget", new ForgetRequest(forgotten));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ForgetResultDto>();
        Assert.Equal((1, 1), (result!.Files, result.FactRows));
        var events = Events(host, id);
        Assert.DoesNotContain(events, e => e.ConversationId == forgotten);
        Assert.DoesNotContain(events, e => e.Text.Contains("surprise party", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Kind == MemoryEventKind.Corrected && e.Text.StartsWith("Forgot a conversation", StringComparison.Ordinal));
        var files = await person.GetFromJsonAsync<List<MemoryFileDto>>("/sample/memory/files");
        Assert.DoesNotContain(files!, f => f.Path == "/notes/secret.md");
        Assert.Contains(files!, f => f.Path == "/notes/shared.md");
        var facts = await Memory(host).CurrentFactsAsync(id, null, CancellationToken.None);
        Assert.Equal(["city"], facts.Value!.Select(f => f.Attribute));
        Assert.DoesNotContain(await person.GetFromJsonAsync<List<ConversationDto>>("/sample/conversations") ?? [], c => c.Id == forgotten);
        Assert.Equal(HttpStatusCode.NotFound, (await person.PostAsJsonAsync("/sample/memory/forget", new ForgetRequest(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_conversation_removes_what_was_said_from_the_log_and_keeps_what_the_memory_made_of_it()
    {
        var llm = new FakeChatClient { Reply = _ => "Saved." };
        await using var host = new SampleHostFactory(postgres, llm);
        var id = Guid.NewGuid();
        var person = host.Person(id);
        var chat = Guid.NewGuid();
        llm.Then(Write("/notes/key.md", "# Key\nUnder the blue pot.\n"));
        await Send(person, chat, "The spare key is under the blue pot.");

        Assert.Equal(HttpStatusCode.NoContent, (await person.DeleteAsync($"/sample/conversations/{chat}")).StatusCode);

        var events = Events(host, id);
        Assert.DoesNotContain(events, e => e.Kind is MemoryEventKind.Said or MemoryEventKind.Answered);
        Assert.Single(events, e => e.Kind == MemoryEventKind.Derived);
        Assert.Contains(await person.GetFromJsonAsync<List<MemoryFileDto>>("/sample/memory/files") ?? [], f => f.Path == "/notes/key.md");
    }

    private static MemoryService Memory(SampleHostFactory host) => host.Services.CreateScope().ServiceProvider.GetRequiredService<MemoryService>();

    private static IReadOnlyList<MemoryEvent> Events(SampleHostFactory host, Guid person) =>
        Memory(host).EventsAsync(person, new EventQuery(), CancellationToken.None).GetAwaiter().GetResult();

    private static async Task Send(HttpClient person, Guid conversation, string content)
    {
        var response = await person.Send(conversation, content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
