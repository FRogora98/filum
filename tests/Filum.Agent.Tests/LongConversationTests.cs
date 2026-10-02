using Filum.Agent.Tests.Infrastructure;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>A conversation longer than the model can read (spec 030): a summary of its start, its latest messages, and nothing lost.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LongConversationTests(PostgresFixture postgres)
{
    private static readonly Dictionary<string, string> Short = new() { ["Memory:MaxHistoryChars"] = "400" };

    [Fact]
    public async Task Past_the_budget_the_start_is_summarized_once_and_its_first_message_is_still_found()
    {
        var summaries = 0;
        var llm = new FakeChatClient
        {
            Reply = messages =>
            {
                if (messages[0].Text.StartsWith(ConversationService.SummaryMarker, StringComparison.Ordinal))
                {
                    summaries++;
                    return "The person planted tomatoes in the north bed.";
                }

                return "Ok.";
            }
        };
        await using var host = new SampleHostFactory(postgres, llm, Short);
        var person = host.Person(Guid.NewGuid());
        var chat = Guid.NewGuid();
        var first = "I planted tomatoes in the north bed of the garden this morning, next to the old apple tree.";
        await Send(person, chat, first);
        for (var i = 1; i <= 4; i++)
        {
            await Send(person, chat, $"Message number {i} about something else entirely, long enough to fill the budget of this test.");
        }

        var sent = llm.Calls[^1].Where(m => m.Role != Microsoft.Extensions.AI.ChatRole.System).ToList();
        Assert.StartsWith("[Filum platform note] Summary of the earlier part of this conversation", sent[0].Text);
        Assert.Contains("The person planted tomatoes in the north bed.", sent[0].Text);
        Assert.DoesNotContain(sent, m => m.Text == first);
        Assert.EndsWith("Message number 4 about something else entirely, long enough to fill the budget of this test.", sent[^1].Text);
        Assert.Equal(sent.Sum(m => m.Text.Length) - sent[0].Text.Length <= 400 * 3 / 4 + 120, true);
        Assert.True(summaries is >= 1 and <= 2, $"{summaries} summaries");

        llm.Then(Call("events_search", new Dictionary<string, object?> { ["query"] = "tomatoes north bed" }));
        await Send(person, chat, "Where did I plant the tomatoes?");
        var found = llm.ToolResultsBefore(llm.Calls.Count - 1).Single(r => r.Contains("north bed of the garden"));
        Assert.Contains(first, found);
    }

    private static async Task Send(HttpClient person, Guid conversation, string content)
    {
        var response = await person.Send(conversation, content);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
