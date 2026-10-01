using Filum.Agent.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>The person's past conversations as a second memory (spec 029), through the sample host.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EpisodicMemoryTests(PostgresFixture postgres)
{
    private static readonly Dictionary<string, string> On = new() { ["Agent:EpisodicMemory"] = "true" };

    private static Dictionary<string, object?> Search(string query, string? from = null, string? to = null) =>
        new() { ["query"] = query, ["from"] = from, ["to"] = to };

    [Fact]
    public async Task A_past_message_is_found_with_its_date_its_conversations_title_and_the_answer_after_it_and_never_another_persons()
    {
        var llm = new FakeChatClient { Reply = _ => "Noted." };
        await using var host = new SampleHostFactory(postgres, llm, On);
        var person = host.Person(Guid.NewGuid());
        var stranger = host.Person(Guid.NewGuid());
        await Send(person, Guid.NewGuid(), "I adopted a grey cat named Miso last week.");
        await Send(stranger, Guid.NewGuid(), "My cat is called Pepper.");

        llm.Then(Call(EpisodicMemory.ToolName, Search("cat name")));
        await Send(person, Guid.NewGuid(), "What is my cat called?");

        var found = Assert.Single(llm.ToolResultsBefore(llm.Calls.Count - 1));
        Assert.Contains("I adopted a grey cat named Miso last week.", found);
        Assert.Contains($"{DateTimeOffset.UtcNow:yyyy-MM-dd}", found);
        Assert.Contains("\"I adopted a grey cat named Miso last week.\"", found);
        Assert.Contains("(then you: Noted.)", found);
        Assert.DoesNotContain("Pepper", found);
        Assert.Contains(EpisodicMemory.Rule.Trim()[..20], llm.InstructionsOf(llm.Calls.Count - 1));
    }

    [Fact]
    public async Task The_current_conversation_and_deleted_ones_are_not_searched_and_a_window_of_days_is_kept()
    {
        var llm = new FakeChatClient { Reply = _ => "Ok." };
        await using var host = new SampleHostFactory(postgres, llm, On);
        var person = host.Person(Guid.NewGuid());
        var deleted = Guid.NewGuid();
        await Send(person, deleted, "The spare key is under the blue pot.");
        Assert.Equal(HttpStatusCode.NoContent, (await person.DeleteAsync($"/sample/conversations/{deleted}")).StatusCode);
        var current = Guid.NewGuid();
        await Send(person, current, "The red bicycle needs new brakes.");

        llm.Then(Call(EpisodicMemory.ToolName, Search("key bicycle")));
        await Send(person, current, "Where is the key?");
        Assert.Equal("No past conversation matches.", Assert.Single(llm.ToolResultsBefore(llm.Calls.Count - 1)));

        await Send(person, Guid.NewGuid(), "The red bicycle is in the garage.");
        llm.Then(Call(EpisodicMemory.ToolName, Search("bicycle", from: "2000-01-01", to: "2000-12-31")));
        await Send(person, Guid.NewGuid(), "Where is the bicycle?");
        Assert.Equal("No past conversation matches.", Assert.Single(llm.ToolResultsBefore(llm.Calls.Count - 1)));
    }

    [Fact]
    public async Task Off_by_default_the_tool_is_not_offered()
    {
        var llm = new FakeChatClient();
        await using var host = new SampleHostFactory(postgres, llm);

        await Send(host.Person(Guid.NewGuid()), Guid.NewGuid(), "Hello");

        Assert.DoesNotContain(llm.Options[0]!.Tools!, t => t.Name == EpisodicMemory.ToolName);
        Assert.DoesNotContain("# Past conversations", llm.InstructionsOf(0));
    }

    private static async Task Send(HttpClient person, Guid conversation, string content)
    {
        var response = await person.Send(conversation, content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
