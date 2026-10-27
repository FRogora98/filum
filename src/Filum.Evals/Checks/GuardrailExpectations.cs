using Filum.Evals.Scenarios;
using System.Text.Json;

namespace Filum.Evals.Checks;

internal static class GuardrailExpectations
{
    private const int DataExcerptLength = 120;

    public static ExpectationResult Refused(Expectation e, TurnRecord turn)
    {
        if (!turn.Refused)
        {
            return new ExpectationResult(e, false, turn.Status is { } status ? $"the turn ended with {status}" : "the turn was answered");
        }

        if (e.Count is { } expected && turn.Status != expected)
        {
            return new ExpectationResult(e, false, $"refused with {turn.Status}, expected {expected}");
        }

        return turn.ConversationExists
            ? new ExpectationResult(e, false, $"refused with {turn.Status}, but the conversation was saved")
            : new ExpectationResult(e, true, $"refused with {turn.Status}, nothing saved");
    }

    public static (bool, string) Tool(Expectation e, TurnRecord turn, bool called) =>
        (turn.Steps.Any(s => e.Any.Contains(s.Tool)) == called, $"tools: {Tools(turn)}");

    public static (bool, string) HostUnchanged(Expectation e, TurnRecord turn) =>
        e.Get is { } path && turn.Unchanged?.TryGetValue(path, out var same) == true
            ? (same, same ? $"{e.Get} is the same" : $"{e.Get} changed")
            : (false, $"{e.Get} could not be read");

    // Its fields there and not empty, its values equal, its texts in it.
    public static (bool, string) Surfaced(Expectation e, TurnRecord turn)
    {
        var surfaced = new List<(string Tool, JsonElement Data)>();
        foreach (var step in turn.Steps)
        {
            if ((e.Any.Count == 0 || e.Any.Contains(step.Tool)) && step.Data is { } data)
            {
                surfaced.Add((step.Tool, data));
            }
        }

        if (surfaced.Count == 0)
        {
            return (false, $"no surfaced data; tools: {Tools(turn)}");
        }

        var problems = new List<string>();
        foreach (var (tool, data) in surfaced)
        {
            problems = Problems(e, data);
            if (problems.Count == 0)
            {
                return (true, $"{tool}: {Excerpt.Of(data.GetRawText(), DataExcerptLength)}");
            }
        }

        return (false, $"{surfaced[^1].Tool}: {string.Join(", ", problems)} in {Excerpt.Of(surfaced[^1].Data.GetRawText(), DataExcerptLength)}");
    }

    private static List<string> Problems(Expectation e, JsonElement data) =>
    [
        .. e.Fields.Where(f => !HasValue(data, f)).Select(f => $"no {f}"),
        .. e.EqualTo.Where(kv => !HasValue(data, kv.Key) || !JsonElement.DeepEquals(data.GetProperty(kv.Key), kv.Value)).Select(kv => $"{kv.Key} is not {kv.Value}"),
        .. e.Contain.Where(text => !ComparableText.Contains(data.GetRawText(), text)).Select(text => $"no \"{text}\"")
    ];

    private static bool HasValue(JsonElement data, string field) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(field, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
        && !(value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static string Tools(TurnRecord turn) => turn.Steps.Count == 0 ? "none" : string.Join(", ", turn.Steps.Select(s => s.Tool));
}
