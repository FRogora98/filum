using Filum.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using static Filum.Agent.Tests.Infrastructure.FakeChatClient;

namespace Filum.Agent.Tests;

/// <summary>A product hosting the engine (spec 017), seen through the sample host over HTTP.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HostingTests(PostgresFixture postgres)
{
    private static readonly Dictionary<string, object?> NoArguments = [];

    [Fact]
    public async Task A_host_tool_is_a_step_with_its_surfaced_data_and_the_model_reads_only_the_text()
    {
        var llm = new FakeChatClient().Then(Call("sample_now", NoArguments));
        await using var host = new SampleHostFactory(postgres, llm, new Dictionary<string, string> { ["Agent:TimeZone"] = "Europe/Rome" });
        var person = host.Person(Guid.NewGuid());
        var conversation = Guid.NewGuid();

        var answer = await Answer(await person.Send(conversation, "What time is it?"));

        var step = Assert.Single(answer.AssistantMessage.Steps, s => s.Tool == "sample_now");
        Assert.Equal(StepDto.Used, step.Kind);
        Assert.Equal("Tell the current date and time in the person's time zone", step.Description);
        Assert.Equal("Europe/Rome", step.Data!.Value.GetProperty("timeZone").GetString());
        var toModel = Assert.Single(llm.ToolResultsBefore(1));
        Assert.StartsWith("It is ", toModel);
        Assert.DoesNotContain("timeZone", toModel);

        // Read again later, the step still carries its data: the app's card survives a restart.
        var messages = await person.GetFromJsonAsync<List<MessageDto>>($"/sample/conversations/{conversation}/messages");
        Assert.Equal("Europe/Rome", messages![1].Steps.Single(s => s.Tool == "sample_now").Data!.Value.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task A_host_tool_named_like_an_engine_tool_stops_the_host_at_start()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient(), services: s => s.AddScoped<ITurnToolSource>(_ => new SomeTools(HostTools.Create(() => "x", "memory_read", "Clashes."))));

        var failure = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("'memory_read'", failure.ToString());
    }

    [Fact]
    public async Task A_host_tool_that_throws_is_a_failed_step_and_the_turn_still_answers_without_its_detail()
    {
        var llm = new FakeChatClient().Then(Call("test_boom", NoArguments));
        await using var host = new SampleHostFactory(postgres, llm, services: s => s.AddScoped<ITurnToolSource>(_ => new SomeTools(
            HostTools.Create(string () => throw new InvalidOperationException("secret detail 42"), "test_boom", "Always fails."))));

        var response = await host.Person(Guid.NewGuid()).Send(Guid.NewGuid(), "Try it");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("secret detail 42", body);
        var step = Assert.Single((await Answer(response)).AssistantMessage.Steps);
        Assert.Equal(StepDto.Failed, step.Kind);
        Assert.DoesNotContain("secret detail 42", Assert.Single(llm.ToolResultsBefore(1)));
    }

    [Fact]
    public async Task A_surfaced_value_over_the_limit_makes_a_failed_step()
    {
        var llm = new FakeChatClient().Then(Call("test_big", NoArguments));
        await using var host = new SampleHostFactory(postgres, llm, services: s => s.AddScoped<ITurnToolSource>(_ => new SomeTools(
            HostTools.Create(() => new SurfacedResult("big", new { blob = new string('x', HostTools.MaxDataBytes + 1000) }), "test_big", "Too much."))));

        var step = Assert.Single((await Answer(await host.Person(Guid.NewGuid()).Send(Guid.NewGuid(), "Go"))).AssistantMessage.Steps);

        Assert.Equal(StepDto.Failed, step.Kind);
        Assert.Equal("The result was too large.", step.Error);
        Assert.Null(step.Data);
    }

    [Fact]
    public async Task The_host_names_its_assistant_and_its_package_rules_come_before_the_core()
    {
        var llm = new FakeChatClient();
        await using var host = new SampleHostFactory(postgres, llm);

        await Answer(await host.Person(Guid.NewGuid()).Send(Guid.NewGuid(), "Hello"));

        var instructions = llm.InstructionsOf(0);
        Assert.Contains("You are Sample, the person's own assistant.", instructions);
        Assert.DoesNotContain("You are Filum", instructions);
        Assert.True(instructions.IndexOf("package \"example\"", StringComparison.Ordinal) < instructions.IndexOf("# The person's core", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_person_is_401_and_two_people_never_see_each_others_conversations()
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient());
        var first = host.Person(Guid.NewGuid());
        var second = host.Person(Guid.NewGuid());
        var conversation = Guid.NewGuid();
        await Answer(await first.Send(conversation, "Mine"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/sample/conversations")).StatusCode);
        Assert.Single((await first.GetFromJsonAsync<List<ConversationDto>>("/sample/conversations"))!);
        Assert.Empty((await second.GetFromJsonAsync<List<ConversationDto>>("/sample/conversations"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/sample/conversations/{conversation}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.Send(conversation, "Theirs?")).StatusCode);
    }

    [Fact]
    public async Task A_gate_that_refuses_ends_the_turn_before_the_model_with_the_hosts_status_and_nothing_is_saved()
    {
        var llm = new FakeChatClient();
        var observed = new Observer();
        await using var host = new SampleHostFactory(postgres, llm, services: s =>
        {
            s.AddScoped<ITurnGate>(_ => new Gate(TurnGateResult.Refuse(402, "Nessun messaggio rimasto questo mese.")));
            s.AddSingleton<ITurnObserver>(observed);
        });
        var person = host.Person(Guid.NewGuid());

        var response = await person.Send(Guid.NewGuid(), "Hello");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Contains("Nessun messaggio rimasto questo mese.", await response.Content.ReadAsStringAsync());
        Assert.Empty(llm.Calls);
        Assert.Empty((await person.GetFromJsonAsync<List<ConversationDto>>("/sample/conversations"))!);
        Assert.Empty(observed.Turns);
    }

    [Fact]
    public async Task The_observer_hears_only_turns_that_answered()
    {
        var llm = new FakeChatClient();
        var observed = new Observer();
        await using var host = new SampleHostFactory(postgres, llm, services: s => s.AddSingleton<ITurnObserver>(observed));
        var person = Guid.NewGuid();

        await Answer(await host.Person(person).Send(Guid.NewGuid(), "Hello"));
        llm.Fail = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await host.Person(person).Send(Guid.NewGuid(), "Again")).StatusCode);

        var (turn, usage) = Assert.Single(observed.Turns);
        Assert.Equal(person, turn.Person);
        Assert.Equal("gpt-5.4-mini", usage.Model);
        Assert.True(usage.CostUsd > 0);
    }

    [Theory]
    [InlineData(true, "gpt-5.4-nano")]
    [InlineData(false, "gpt-5.4-mini")]
    public async Task The_host_decides_whether_a_request_may_choose_the_model(bool allowed, string used)
    {
        await using var host = new SampleHostFactory(postgres, new FakeChatClient(), new Dictionary<string, string> { ["Agent:AllowModelChoice"] = allowed.ToString() });

        await Answer(await host.Person(Guid.NewGuid()).Send(Guid.NewGuid(), "Hello", model: "gpt-5.4-nano"));

        Assert.Equal(used, Assert.Single(host.Models.RequestedModels));
    }

    private static async Task<SendMessageResponse> Answer(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SendMessageResponse>())!;
    }

    private sealed class SomeTools(params AIFunction[] functions) : ITurnToolSource
    {
        public IReadOnlyList<AIFunction> Tools(TurnContext turn) => functions;
    }

    private sealed class Gate(TurnGateResult result) : ITurnGate
    {
        public Task<TurnGateResult> CheckAsync(TurnContext turn) => Task.FromResult(result);
    }

    private sealed class Observer : ITurnObserver
    {
        public List<(TurnContext Turn, TurnUsage Usage)> Turns { get; } = [];

        public Task AnsweredAsync(TurnContext turn, TurnUsage usage)
        {
            Turns.Add((turn, usage));
            return Task.CompletedTask;
        }
    }
}
