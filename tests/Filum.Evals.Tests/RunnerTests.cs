using Filum.Agent.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Evals.Tests;

/// <summary>The runner against the sample host (spec 018), with a fake model and a scripted judge: no money, no network.</summary>
[Collection(PostgresCollection.Name)]
public sealed class RunnerTests(PostgresFixture postgres)
{
    private static readonly HostTarget Sample = new("/sample", Register: null, PersonHeader: "X-Sample-Person");

    private static Scenario Scenario(string expectations, string message = "Keep a note: the plant needs water on Mondays.") =>
        ScenarioLoader.Parse($$"""{"id":"s","language":"en","turns":[{"chat":"a","message":"{{message}}","expect":[{{expectations}}]}]}""", "s.json");

    [Fact]
    public async Task A_host_with_a_person_header_and_its_own_model_is_played_with_a_new_person_every_run()
    {
        var llm = new FakeChatClient { Reply = _ => "Noted." }
            .Then(Call("memory_write", new Dictionary<string, object?> { ["path"] = "/notes/plant.md", ["content"] = "Water on Mondays.\n" }));
        await using var host = new SampleHostFactory(postgres, llm);
        var runner = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample);
        var scenario = Scenario("""{"type":"wrote"},{"type":"memory_contains","any":["Mondays"]}""");

        var result = await new EvalRun(runner, capUsd: 5, target: Sample.Describe(host.Server.BaseAddress)).RunAsync([scenario], [HostTarget.HostModel], repetitions: 2, CancellationToken.None);

        Assert.All(host.Models.RequestedModels, m => Assert.Equal("gpt-5.4-mini", m));
        Assert.True(result.Runs[0].Succeeded, string.Join("; ", result.Runs[0].Checks.Select(c => c.Result.Detail)));
        // The second run is a new person: the note of the first is not theirs, so this time nothing was written.
        Assert.False(result.Runs[1].Succeeded);
        var markdown = EvalReport.Markdown(result);
        Assert.Contains("| host |", markdown);
        Assert.Contains("Host: ", markdown);
        Assert.Contains("| 2 / 0 |", markdown);
    }

    [Fact]
    public async Task A_host_without_the_memory_group_is_still_evaluated_on_the_rest()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient { Reply = _ => "Hello there." }, new Dictionary<string, string> { ["Sample:MapMemory"] = "false" });
        var runner = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample);

        var run = await runner.RunAsync(Scenario("""{"type":"core_contains","any":["x"]},{"type":"answer_contains","any":["hello"]}""", "Hi"), HostTarget.HostModel, 1, CancellationToken.None);

        Assert.Equal(("the host maps no memory group", false), (run.Checks[0].Result.Detail, run.Checks[0].Result.Passed));
        Assert.True(run.Checks[1].Result.Passed);
    }

    [Fact]
    public async Task A_refusing_gate_is_checked_with_its_status_and_counted_as_refused()
    {
        var llm = new FakeChatClient();
        await using var host = new SampleHostFactory(postgres, llm, services: s => s.AddScoped<ITurnGate>(_ => new Refuse()));
        var runner = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample);

        var result = await new EvalRun(runner, capUsd: 5).RunAsync([Scenario("""{"type":"refused","count":402}""", "Hi")], [HostTarget.HostModel], 1, CancellationToken.None);

        Assert.True(result.Runs[0].Succeeded, result.Runs[0].Checks[0].Result.Detail);
        Assert.Empty(llm.Calls);
        Assert.Contains("| 0 / 1 |", EvalReport.Markdown(result));
    }

    [Fact]
    public async Task Host_unchanged_reads_the_hosts_path_before_and_after_the_turn()
    {
        var writes = new FakeChatClient { Reply = _ => "Done.", UseToolCallsWhen = messages => messages.Any(m => m.Text.Contains("Write it")) }
            .Then(Call("memory_write", new Dictionary<string, object?> { ["path"] = "/notes/a.md", ["content"] = "a\n" }));
        await using var host = new SampleHostFactory(postgres, writes);
        var runner = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample);
        const string unchanged = """{"type":"host_unchanged","get":"/sample/memory/files"}""";

        // A new person's first turn creates their memory (the core, the starter skills), so both checks are on a second turn.
        var twoTurns = (string second) => ScenarioLoader.Parse($$"""
            {"id":"u","language":"en","turns":[{"chat":"a","message":"Hi","expect":[]},{"chat":"a","message":"{{second}}","expect":[{{unchanged}}]}]}
            """, "u.json");

        var same = await runner.RunAsync(twoTurns("Hi again"), HostTarget.HostModel, 1, CancellationToken.None);
        var changed = await runner.RunAsync(twoTurns("Write it"), HostTarget.HostModel, 1, CancellationToken.None);

        Assert.True(same.Checks[0].Result.Passed, same.Checks[0].Result.Detail);
        Assert.Equal("/sample/memory/files changed", changed.Checks[0].Result.Detail);
    }

    [Fact]
    public async Task A_judge_expectation_asks_the_scenarios_question_and_its_cost_counts()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient { Reply = _ => "Ciao! Come posso aiutarti?" });
        var judge = new ScriptedJudge { Yes = (question, answer) => question.Contains("Italian") && answer.Contains("Ciao") };
        var runner = new ScenarioRunner(host.CreateClient(), judge, Sample);

        var run = await runner.RunAsync(Scenario("""{"type":"judge","question":"Is the reply written in Italian?","pass_if":"yes"},{"type":"judge","question":"Is the reply in German?","pass_if":"yes"}""", "Ciao"), HostTarget.HostModel, 1, CancellationToken.None);

        Assert.Equal([true, false], run.Checks.Select(c => c.Result.Passed));
        Assert.Equal(["Is the reply written in Italian?", "Is the reply in German?"], judge.Questions);
        Assert.Equal(3 * ScriptedJudge.Cost, run.JudgeCostUsd);
    }

    [Fact]
    public async Task A_setup_command_runs_for_each_new_person_and_its_failure_stops_the_run()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient());
        var marker = Path.Combine(Path.GetTempPath(), $"filum-setup-{Guid.NewGuid():N}.txt");
        try
        {
            var ok = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample with { Setup = $"echo {{person}} {{email}}> \"{marker}\"" });
            var run = await ok.RunAsync(Scenario("""{"type":"answer_contains","any":["fake"]}""", "Hi"), HostTarget.HostModel, 1, CancellationToken.None);
            Assert.True(run.Succeeded);
            Assert.Contains("@example.invalid", File.ReadAllText(marker));

            var failing = new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample with { Setup = "exit 3" });
            var stopped = await failing.RunAsync(Scenario("""{"type":"answer_contains","any":["fake"]}""", "Hi"), HostTarget.HostModel, 1, CancellationToken.None);
            Assert.False(stopped.Succeeded);
            Assert.Empty(stopped.Turns);
            Assert.StartsWith("setup exited with 3", stopped.SetupError);
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact]
    public async Task Every_synthetic_scenario_loads_and_plays_without_runner_errors()
    {
        var scenarios = ScenarioLoader.LoadAll(Path.Combine(AppContext.BaseDirectory, "scenarios"));
        await using var host = new SampleHostFactory(postgres, new FakeChatClient());

        var result = await new EvalRun(new ScenarioRunner(host.CreateClient(), new ScriptedJudge(), Sample), capUsd: 5)
            .RunAsync(scenarios, [HostTarget.HostModel], repetitions: 1, CancellationToken.None);

        Assert.True(scenarios.Count >= 17, $"{scenarios.Count} scenarios");
        Assert.All(result.Runs.SelectMany(r => r.Turns), t => Assert.Null(t.Error));
        Assert.Equal(scenarios.Sum(s => s.TurnCount), result.Runs.Sum(r => r.Turns.Count));
    }

    private sealed class Refuse : ITurnGate
    {
        public Task<TurnGateResult> CheckAsync(TurnContext turn) => Task.FromResult(TurnGateResult.Refuse(402, "No messages left."));
    }
}

/// <summary>Claims a change when the answer says "saved"; answers questions by a script; each call costs a fixed amount.</summary>
public sealed class ScriptedJudge : IClaimJudge
{
    public const decimal Cost = 0.0001m;

    public Func<string, string, bool> Yes { get; init; } = (_, _) => false;

    public List<string> Questions { get; } = [];

    public Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken) =>
        Task.FromResult(new ClaimVerdict(answer.Contains("saved", StringComparison.OrdinalIgnoreCase), Cost));

    public Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken)
    {
        Questions.Add(question);
        return Task.FromResult(new JudgeAnswer(Yes(question, answer), Cost));
    }
}
