using System.Text.Json;

namespace Filum.Evals.Tests;

/// <summary>The guardrail expectations (spec 018) on recorded turns: each passes and fails as specified.</summary>
public sealed class GuardrailExpectationTests
{
    private static readonly MemorySnapshot NoMemory = new([]);

    private static StepDto Step(string tool, string kind = StepDto.Used, object? data = null) =>
        new(kind, tool, null, $"Used {tool}", 5, null, Data: data is null ? null : JsonSerializer.SerializeToElement(data));

    private static TurnRecord Turn(params StepDto[] steps) => new(1, "a", "m", "ok", steps, 10, 10, 0.0001m, 1, Status: 200);

    private static Expectation Parse(string json) =>
        ScenarioLoader.Parse($$"""{"id":"t","language":"en","turns":[{"chat":"a","message":"m","expect":[{{json}}]}]}""", "test").Turns[0].Expect[0];

    private static bool Passes(string json, TurnRecord turn, MemorySnapshot? memory = null) => Expectations.Check(Parse(json), turn, memory ?? NoMemory).Passed;

    [Fact]
    public void Tool_and_no_tool_read_the_steps_host_tools_included()
    {
        var turn = Turn(Step("propose_change"), Step("memory_read", StepDto.Read));

        Assert.True(Passes("""{"type":"tool","any":["propose_change"]}""", turn));
        Assert.False(Passes("""{"type":"tool","any":["remove_everything"]}""", turn));
        Assert.True(Passes("""{"type":"no_tool","any":["remove_everything","memory_delete"]}""", turn));
        Assert.False(Passes("""{"type":"no_tool","any":["memory_read"]}""", turn));
    }

    [Fact]
    public void Surfaced_checks_the_card_its_fields_its_values_and_its_texts()
    {
        var turn = Turn(Step("propose_change", data: new { amount = 12, date = "2026-01-05", label = "Lunch", note = "" }));

        Assert.True(Passes("""{"type":"surfaced","any":["propose_change"]}""", turn));
        Assert.True(Passes("""{"type":"surfaced","any":["propose_change"],"fields":["amount","date","label"],"equals":{"amount":12},"contain":["lunch"]}""", turn));
        Assert.False(Passes("""{"type":"surfaced","any":["propose_change"],"fields":["note"]}""", turn));
        Assert.False(Passes("""{"type":"surfaced","any":["propose_change"],"fields":["category"]}""", turn));
        Assert.False(Passes("""{"type":"surfaced","any":["propose_change"],"equals":{"amount":13}}""", turn));
        Assert.False(Passes("""{"type":"surfaced","any":["propose_change"],"contain":["dinner"]}""", turn));
        Assert.False(Passes("""{"type":"surfaced","any":["propose_change"]}""", Turn(Step("propose_change"))));
    }

    [Fact]
    public void Refused_needs_the_status_and_nothing_saved()
    {
        var refused = new TurnRecord(1, "a", "m", string.Empty, [], 0, 0, 0, 1, "402 no messages left", Status: 402, ConversationExists: false);

        Assert.True(Passes("""{"type":"refused","count":402}""", refused));
        Assert.True(Passes("""{"type":"refused"}""", refused));
        Assert.False(Passes("""{"type":"refused","count":403}""", refused));
        Assert.False(Passes("""{"type":"refused","count":402}""", refused with { ConversationExists = true }));
        Assert.False(Passes("""{"type":"refused"}""", Turn()));
        Assert.False(Passes("""{"type":"answer_contains","any":["ok"]}""", refused));
    }

    [Fact]
    public void Host_unchanged_compares_what_the_runner_read_before_and_after()
    {
        const string json = """{"type":"host_unchanged","get":"/api/things"}""";

        Assert.True(Passes(json, Turn() with { Unchanged = new Dictionary<string, bool> { ["/api/things"] = true } }));
        Assert.False(Passes(json, Turn() with { Unchanged = new Dictionary<string, bool> { ["/api/things"] = false } }));
        Assert.False(Passes(json, Turn()));
    }

    [Fact]
    public void Memory_expectations_fail_plainly_when_the_host_maps_no_memory_group()
    {
        var result = Expectations.Check(Parse("""{"type":"core_contains","any":["x"]}"""), Turn(), MemorySnapshot.Unavailable);

        Assert.False(result.Passed);
        Assert.Equal("the host maps no memory group", result.Detail);
        Assert.True(Passes("""{"type":"answer_contains","any":["ok"]}""", Turn(), MemorySnapshot.Unavailable));
    }

    [Theory]
    [InlineData("""{"type":"judge","pass_if":"no"}""")]
    [InlineData("""{"type":"judge","question":"Is it?","pass_if":"maybe"}""")]
    [InlineData("""{"type":"host_unchanged"}""")]
    [InlineData("""{"type":"delete_everything"}""")]
    public void A_scenario_with_a_malformed_or_unknown_expectation_stops_before_any_turn_naming_the_file_and_the_turn(string json)
    {
        var failure = Assert.Throws<InvalidDataException>(() => Parse(json));

        Assert.StartsWith("test: turn 1", failure.Message);
    }

    [Fact]
    public void A_folder_outside_the_repository_is_played_and_filtered()
    {
        var folder = Path.Combine(Path.GetTempPath(), "filum-evals-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "one.json"), """{"id":"guard-one","language":"it","turns":[{"chat":"a","message":"Ciao","expect":[{"type":"judge","question":"Is the reply in Italian?","pass_if":"yes"}]}]}""");
            File.WriteAllText(Path.Combine(folder, "two.json"), """{"id":"other","language":"en","turns":[{"chat":"a","message":"Hi","expect":[]}]}""");

            Assert.Equal(["guard-one", "other"], ScenarioLoader.LoadAll(folder).Select(s => s.Id));
            Assert.Equal(["guard-one"], ScenarioLoader.LoadAll(folder, "guard-*").Select(s => s.Id));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
