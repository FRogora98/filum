using Filum.Agent.Tests.Infrastructure;
using Filum.Evals.LongMemEval;
using Microsoft.Extensions.AI;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Evals.Tests;

/// <summary>The LongMemEval benchmark's logic (spec 020), on a small synthetic file in the benchmark's format: no model, no key.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LongMemEvalTests(PostgresFixture postgres) : IDisposable
{
    private const string File3 = """
        [
          {"question_id": "t1", "question_type": "temporal-reasoning", "question": "Which came first, the bike or the boat?", "answer": "The bike",
           "question_date": "2023/05/01 (Mon) 10:00", "haystack_session_ids": ["b", "a"], "haystack_dates": ["2023/04/10 (Mon) 17:50", "2023/04/10 (Mon) 14:47"],
           "haystack_sessions": [[{"role": "user", "content": "I bought a boat today."}], [{"role": "user", "content": "I bought a bike today.", "has_answer": true}, {"role": "assistant", "content": "Nice!"}]],
           "answer_session_ids": ["a"]},
          {"question_id": "u1_abs", "question_type": "single-session-user", "question": "What is my cat's name?", "answer": "You never said.",
           "question_date": "2023/05/02 (Tue) 09:00", "haystack_session_ids": ["c"], "haystack_dates": ["2023/04/01 (Sat) 08:00"],
           "haystack_sessions": [[{"role": "user", "content": "I have a dog named Rex."}]], "answer_session_ids": []},
          {"question_id": "k1", "question_type": "knowledge-update", "question": "How many plants do I have now?", "answer": 4,
           "question_date": "2023/06/01 (Thu) 09:00", "haystack_session_ids": ["d", "e"], "haystack_dates": ["2023/04/01 (Sat) 08:00", "2023/05/01 (Mon) 08:00"],
           "haystack_sessions": [[{"role": "user", "content": "I have 3 plants."}], [{"role": "user", "content": "I bought another plant, so 4 now."}]], "answer_session_ids": ["e"]}
        ]
        """;

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"lme-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    private async Task<IReadOnlyList<LmeInstance>> Load()
    {
        await File.WriteAllTextAsync(_file, File3);
        return await LmeDataset.LoadAsync(_file);
    }

    [Fact]
    public async Task The_reader_maps_abilities_reads_number_answers_and_puts_sessions_in_date_order()
    {
        var instances = await Load();

        Assert.Equal([LmeAbilities.Temporal, LmeAbilities.Abstention, LmeAbilities.KnowledgeUpdate], instances.Select(i => i.Ability));
        Assert.Equal("4", instances[2].AnswerText);
        // The file has the boat's session first, at 17:50; the bike's, at 14:47, came first.
        Assert.Equal(["2023/04/10 (Mon) 14:47", "2023/04/10 (Mon) 17:50"], instances[0].Sessions.Select(s => s.Date));
        Assert.Contains("bike", instances[0].Sessions.First().Turns[0].Content);
    }

    [Fact]
    public async Task A_subset_has_the_same_number_per_ability_and_the_same_seed_gives_the_same_ids()
    {
        var three = await Load();
        var many = Enumerable.Range(0, 40).Select(n => three[n % 3] with { QuestionId = $"q{n}{(n % 3 == 1 ? "_abs" : "")}" }).ToList();

        var first = LmeSubset.Build(many, perAbility: 4, seed: 7);
        var again = LmeSubset.Build(many, perAbility: 4, seed: 7);
        var other = LmeSubset.Build(many, perAbility: 4, seed: 8);

        Assert.Equal(12, first.QuestionIds.Count);
        Assert.All(many.Where(i => first.QuestionIds.Contains(i.QuestionId)).GroupBy(i => i.Ability), g => Assert.Equal(4, g.Count()));
        Assert.Equal(first.QuestionIds, again.QuestionIds);
        Assert.NotEqual(first.QuestionIds, other.QuestionIds);
    }

    [Fact]
    public void A_long_session_is_sent_in_parts_none_longer_than_a_message_and_nothing_lost()
    {
        var turns = Enumerable.Range(0, 60).Select(n => new LmeTurn(n % 2 == 0 ? "user" : "assistant", new string('a', 200) + n)).ToList();

        var parts = LmeProtocol.SessionMessages("2023/04/10 (Mon) 14:47", turns);

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= LmeProtocol.MaxMessage, $"{p.Length}"));
        Assert.StartsWith("Here is a conversation you had with the person on 2023/04/10 (Mon) 14:47.", parts[0]);
        Assert.Contains($"(part 1 of {parts.Count})", parts[0]);
        Assert.All(Enumerable.Range(0, 60), n => Assert.Contains(parts, p => p.Contains(new string('a', 200) + n + "\n") || p.EndsWith(new string('a', 200) + n)));
        Assert.Single(LmeProtocol.SessionMessages("d", [new LmeTurn("user", "short")]));
    }

    [Fact]
    public async Task Each_ability_is_judged_with_the_authors_prompt()
    {
        var instances = await Load();

        Assert.Contains("do not penalize off-by-one errors", LmeJudge.Prompt(instances[0], "the bike"));
        Assert.Contains("Does the model correctly identify the question as unanswerable?", LmeJudge.Prompt(instances[1], "I don't know"));
        Assert.Contains("updated answer is the required answer", LmeJudge.Prompt(instances[2], "4"));
        Assert.Contains("Correct Answer: 4", LmeJudge.Prompt(instances[2], "4"));
        Assert.Throws<NotSupportedException>(() => LmeJudge.Prompt(instances[2] with { QuestionType = "made-up" }, "x"));
    }

    [Fact]
    public void Bm25_ranks_the_session_that_matches_the_question_first()
    {
        var top = Bm25.Top(["We talked about the weather and the rain.", "I adopted a cat named Miso last week.", "The cat food is on sale."], "What is my cat's name?", 2);

        Assert.Equal(2, top.Count);
        Assert.Contains(1, top);
        Assert.DoesNotContain(0, top);
    }

    [Fact]
    public async Task On_a_host_each_session_is_a_new_chat_in_date_order_and_the_question_comes_last_with_its_date()
    {
        var llm = new FakeChatClient { Reply = _ => "The bike came first." };
        await using var host = new SampleHostFactory(postgres, llm);
        var system = new HostLmeSystem(host.CreateClient(), new HostTarget("/sample", Register: null, PersonHeader: "X-Sample-Person"), HostTarget.HostModel, "filum");

        var answer = await system.AnswerAsync((await Load())[0], CancellationToken.None);

        Assert.Null(answer.Error);
        Assert.Equal("The bike came first.", answer.Answer);
        Assert.Equal(3, answer.Turns);
        var lastUserMessages = llm.Calls.Select(c => c.Last(m => m.Role == ChatRole.User).Text).ToList();
        Assert.Contains("bike", lastUserMessages[0]);
        Assert.Contains("boat", lastUserMessages[1]);
        Assert.Equal("Today is 2023/05/01 (Mon) 10:00. Which came first, the bike or the boat?", lastUserMessages[2]);
        // A new chat each time: no call carries an earlier session.
        Assert.DoesNotContain("bike", lastUserMessages[1]);
        Assert.Single(llm.Calls[2], m => m.Role == ChatRole.User);
        Assert.True(answer.CostUsd > 0);
    }

    [Fact]
    public async Task The_references_ask_the_same_question_with_nothing_or_with_the_best_sessions()
    {
        var instance = (await Load())[2];
        var fake = new RecordingClient();
        var model = new DirectModel("small", fake, 1m, 2m);

        await new NoMemorySystem(model).AnswerAsync(instance, CancellationToken.None);
        await new NaiveRetrievalSystem(model, k: 1).AnswerAsync(instance, CancellationToken.None);

        Assert.Equal("Today is 2023/06/01 (Thu) 09:00. How many plants do I have now?", fake.Prompts[0]);
        // BM25 picks the older session ("I have 3 plants" matches more words) and misses the update: the very weakness of
        // naive retrieval on knowledge updates that the benchmark is there to show.
        Assert.Contains("[Conversation on 2023/04/01 (Sat) 08:00]", fake.Prompts[1]);
        Assert.DoesNotContain("2023/05/01 (Mon) 08:00", fake.Prompts[1]);
        Assert.EndsWith("Today is 2023/06/01 (Thu) 09:00. How many plants do I have now?", fake.Prompts[1]);
    }

    [Fact]
    public async Task A_run_judges_every_answer_reports_accuracy_over_repetitions_and_stops_at_its_caps()
    {
        var instances = await Load();
        var judge = new SayJudge();
        var system = new FixedSystem("x", sessions: 2);

        var run = await new LmeRun(judge, capUsd: 10).RunAsync(instances, [system], repetitions: 2, CancellationToken.None);
        var score = Assert.Single(LmeReport.Score(run));

        Assert.Equal(6, run.Results.Count);
        Assert.Equal(6, judge.Prompts.Count);
        Assert.Equal((3, 2), (score.Questions, score.Repetitions));
        Assert.Equal(1.0 / 3, score.Accuracy, 3);
        Assert.Equal(1.0, score.ByAbility[LmeAbilities.Temporal]);
        Assert.Contains("| x | fixed | 3 × 2 |", LmeReport.Markdown(run));

        var bySessions = await new LmeRun(judge, capUsd: 10, maxSessions: 3).RunAsync(instances, [system], 1, CancellationToken.None);
        Assert.True(bySessions.Stopped);
        Assert.Single(bySessions.Results);

        var byDollars = await new LmeRun(judge, capUsd: 0.000001m).RunAsync(instances, [system], 1, CancellationToken.None);
        Assert.True(byDollars.Stopped);
        Assert.Single(byDollars.Results);
    }

    [Fact]
    public async Task A_run_with_a_journal_writes_every_answer_at_once_and_resumes_without_playing_them_again()
    {
        var instances = await Load();
        var journal = Path.Combine(Path.GetTempPath(), $"lme-journal-{Guid.NewGuid():N}.jsonl");
        var judge = new SayJudge();
        try
        {
            var stopped = await new LmeRun(judge, capUsd: 10, maxSessions: 4, journal: journal).RunAsync(instances, [new FixedSystem("x", sessions: 2)], 1, CancellationToken.None);
            Assert.Equal(2, stopped.Results.Count);
            Assert.Equal(2, File.ReadAllLines(journal).Length);

            var resumed = await new LmeRun(judge, capUsd: 10, journal: journal).RunAsync(instances, [new FixedSystem("x", sessions: 2)], 1, CancellationToken.None);

            Assert.Equal(3, resumed.Results.Count);
            Assert.Equal(3, judge.Prompts.Count);
            Assert.Equal(instances.Select(i => i.QuestionId), resumed.Results.Select(r => r.QuestionId));
        }
        finally
        {
            File.Delete(journal);
        }
    }

    [Fact]
    public async Task Claude_with_filum_takes_one_session_per_history_session_and_one_for_the_question()
    {
        var agent = new PromptAgent();
        var instance = (await Load())[0];
        var withFilum = new ClaudeLmeSystem(agent, ClaudeLmeSystem.Mode.WithFilum, "filum-mcp", "sonnet");
        var full = new ClaudeLmeSystem(agent, ClaudeLmeSystem.Mode.FullContext, "filum-mcp", "sonnet");

        Assert.Equal(3, withFilum.SessionsFor(instance));
        await withFilum.AnswerAsync(instance, CancellationToken.None);
        Assert.Equal([true, true, true], agent.Sessions.Select(s => s.Filum));
        Assert.Contains("bike", agent.Sessions[0].Message);
        Assert.StartsWith("Today is", agent.Sessions[2].Message);

        agent.Sessions.Clear();
        await full.AnswerAsync(instance, CancellationToken.None);
        var only = Assert.Single(agent.Sessions);
        Assert.False(only.Filum);
        Assert.Contains("I bought a boat today.", only.Message);
    }

    private sealed class RecordingClient : IChatClient
    {
        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Prompts.Add(messages.Last().Text);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "4")) { Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 5 } });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    /// <summary>Says yes only to the temporal question's prompt.</summary>
    private sealed class SayJudge : IPromptJudge
    {
        public List<string> Prompts { get; } = [];

        public Task<(bool Yes, decimal CostUsd)> YesAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult((prompt.Contains("off-by-one"), 0.0001m));
        }
    }

    private sealed class FixedSystem(string name, int sessions) : ILmeSystem
    {
        public string Name => name;

        public string Model => "fixed";

        public int SessionsFor(LmeInstance instance) => sessions;

        public Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken) =>
            Task.FromResult(new LmeAnswer("an answer", 10, 1, 0.001m, 0.1, 2));
    }

    private sealed class PromptAgent : IAgent
    {
        public string Name => "prompt agent";

        public List<(string Message, bool Filum)> Sessions { get; } = [];

        public Task<AgentSession> AskAsync(string message, string workingDirectory, string mcpConfig, bool filumMounted, CancellationToken cancellationToken)
        {
            Sessions.Add((message, filumMounted));
            return Task.FromResult(new AgentSession("ok", [], null, "claude-sonnet-test"));
        }
    }
}
