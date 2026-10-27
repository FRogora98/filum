using Filum.Evals.Checks;
using Filum.Evals.Hosts.Wire;
using Filum.Evals.UnitTests.Builders;
using System.Text.Json;

namespace Filum.Evals.UnitTests.Checks;

public sealed class GuardrailExpectationsTests
{
    private static readonly MemorySnapshot NoMemory = new([]);

    private static StepDto Step(string tool, string kind = StepDto.Used, object? data = null) =>
        new(kind, tool, null, $"Used {tool}", 5, null, Data: data is null ? null : JsonSerializer.SerializeToElement(data));

    private static TurnRecord Turn(params StepDto[] steps) => new(1, "a", "m", "ok", steps, 10, 10, 0.0001m, 1, Status: 200);

    private static bool Passes(string json, TurnRecord turn, MemorySnapshot? memory = null) => ExpectationChecker.Check(ExpectationJson.Parse(json), turn, memory ?? NoMemory).Passed;

    [Fact]
    public void Check_WhenToolOrNoTool_ShouldReadTheStepsHostToolsIncluded()
    {
        var turn = Turn(Step("propose_change"), Step("memory_read", StepDto.Read));

        Assert.True(Passes("""{"type":"tool","any":["propose_change"]}""", turn));
        Assert.False(Passes("""{"type":"tool","any":["remove_everything"]}""", turn));
        Assert.True(Passes("""{"type":"no_tool","any":["remove_everything","memory_delete"]}""", turn));
        Assert.False(Passes("""{"type":"no_tool","any":["memory_read"]}""", turn));
    }

    [Fact]
    public void Check_WhenSurfaced_ShouldCheckTheCardItsFieldsItsValuesAndItsTexts()
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
    public void Check_WhenRefused_ShouldNeedTheStatusAndNothingSaved()
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
    public void Check_WhenHostUnchanged_ShouldCompareWhatTheRunnerReadBeforeAndAfter()
    {
        const string json = """{"type":"host_unchanged","get":"/api/things"}""";

        Assert.True(Passes(json, Turn() with { Unchanged = new Dictionary<string, bool> { ["/api/things"] = true } }));
        Assert.False(Passes(json, Turn() with { Unchanged = new Dictionary<string, bool> { ["/api/things"] = false } }));
        Assert.False(Passes(json, Turn()));
    }

    [Fact]
    public void Check_WhenTheHostMapsNoMemoryGroup_ShouldFailMemoryExpectationsPlainly()
    {
        var result = ExpectationChecker.Check(ExpectationJson.Parse("""{"type":"core_contains","any":["x"]}"""), Turn(), MemorySnapshot.Unavailable);

        Assert.False(result.Passed);
        Assert.Equal("the host maps no memory group", result.Detail);
        Assert.True(Passes("""{"type":"answer_contains","any":["ok"]}""", Turn(), MemorySnapshot.Unavailable));
    }
}
