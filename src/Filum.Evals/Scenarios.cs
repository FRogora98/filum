using System.Text.Json;
using System.Text.Json.Serialization;

namespace Filum.Evals;

/// <summary>A few messages from a synthetic person, and what must be true after each of them.</summary>
public sealed record Scenario(string Id, string Language, string? Description, IReadOnlyList<ScenarioTurn> Turns)
{
    public int TurnCount => Turns.Count;
}

/// <param name="Chat">A label: a new label is a new conversation, the same label continues it.</param>
public sealed record ScenarioTurn(string Chat, string Message, IReadOnlyList<Expectation> Expect);

/// <summary>Something code can check after a turn. Which fields matter depends on <see cref="Type"/>.</summary>
public sealed record Expectation
{
    public string Type { get; init; } = string.Empty;

    /// <summary>Texts of which one must (or must not) be found.</summary>
    public IReadOnlyList<string> Any { get; init; } = [];

    /// <summary>Words of which one must be in the file's name, since the model picks its own names.</summary>
    [JsonPropertyName("name_any")]
    public IReadOnlyList<string> NameAny { get; init; } = [];

    /// <summary><c>collection</c> or <c>document</c> for <c>file</c>; a step kind for <c>step</c>.</summary>
    public string? Kind { get; init; }

    public string? Sensitivity { get; init; }

    /// <summary>The exact number of rows, for <c>rows</c>.</summary>
    public int? Count { get; init; }

    /// <summary>Values each of which must appear in some row, for <c>rows</c>.</summary>
    public IReadOnlyList<string> Contain { get; init; } = [];

    /// <summary>The most words the answer may have, for <c>answer_max_words</c>.</summary>
    public int? Max { get; init; }

    /// <summary>Whether the skill must be on or off, for <c>skill</c>.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Counts in "rules kept".</summary>
    public bool Rule { get; init; }

    /// <summary>Fields the surfaced data must have, not empty, for <c>surfaced</c>.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>Field → value the surfaced data must have, for <c>surfaced</c>.</summary>
    [JsonPropertyName("equals")]
    public IReadOnlyDictionary<string, JsonElement> EqualTo { get; init; } = new Dictionary<string, JsonElement>();

    /// <summary>A path of the host read before and after the turn, for <c>host_unchanged</c>.</summary>
    public string? Get { get; init; }

    /// <summary>A yes/no question about the answer, for <c>judge</c>; the scenario's own data.</summary>
    public string? Question { get; init; }

    /// <summary><c>yes</c> or <c>no</c>: the judge's answer that passes, for <c>judge</c>.</summary>
    [JsonPropertyName("pass_if")]
    public string? PassIf { get; init; }

    public override string ToString() => Type switch
    {
        "file" => $"file {Kind} named {string.Join("|", NameAny)}{(Sensitivity is null ? "" : $" ({Sensitivity})")}",
        "rows" => $"rows of {string.Join("|", NameAny)}{(Count is null ? "" : $" = {Count}")}{(Contain.Count == 0 ? "" : $" containing {string.Join(", ", Contain)}")}",
        "step" => $"a {Kind} step",
        "answer_max_words" => $"answer at most {Max} words",
        "skill" => $"skill named {string.Join("|", NameAny)}{(Enabled is null ? "" : Enabled.Value ? " (on)" : " (off)")}",
        "no_skill" => NameAny.Count == 0 ? "no skill saved" : $"no skill named {string.Join("|", NameAny)}",
        "proposal" => NameAny.Count == 0 ? "a skill proposed" : $"a skill proposed named {string.Join("|", NameAny)}",
        "wrote" or "no_write" or "used_skill" => Type,
        "tool" => $"a call of {string.Join("|", Any)}",
        "no_tool" => $"no call of {string.Join("|", Any)}",
        "surfaced" => $"surfaced data from {string.Join("|", Any)}{(Fields.Count == 0 ? "" : $" with {string.Join(", ", Fields)}")}{(EqualTo.Count == 0 ? "" : $" where {string.Join(", ", EqualTo.Select(kv => $"{kv.Key} = {kv.Value}"))}")}{(Contain.Count == 0 ? "" : $" containing {string.Join(", ", Contain)}")}",
        "refused" => Count is null ? "the turn refused" : $"the turn refused with {Count}",
        "host_unchanged" => $"{Get} unchanged",
        "judge" => $"the judge says {PassIf} to \"{Question}\"",
        _ => $"{Type} {string.Join("|", Any)}"
    };
}

public static class ScenarioLoader
{
    public static readonly IReadOnlySet<string> Types = new HashSet<string>
    {
        "wrote", "no_write", "step", "answer_contains", "answer_not_contains", "answer_max_words",
        "core_contains", "core_not_contains", "memory_contains", "file", "rows",
        "skill", "no_skill", "used_skill", "proposal",
        "tool", "no_tool", "surfaced", "refused", "host_unchanged", "judge"
    };

    /// <summary>The types that read the person's memory, which a host may not map.</summary>
    public static readonly IReadOnlySet<string> MemoryTypes = new HashSet<string>
    {
        "core_contains", "core_not_contains", "memory_contains", "file", "rows", "skill", "no_skill"
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static Scenario Parse(string json, string source)
    {
        var scenario = JsonSerializer.Deserialize<Scenario>(json, Json) ?? throw new InvalidDataException($"{source}: empty scenario.");
        if (string.IsNullOrWhiteSpace(scenario.Id) || scenario.Turns is null || scenario.Turns.Count == 0)
        {
            throw new InvalidDataException($"{source}: a scenario needs an id and at least one turn.");
        }

        for (var i = 0; i < scenario.Turns.Count; i++)
        {
            var turn = scenario.Turns[i];
            if (string.IsNullOrWhiteSpace(turn.Message) || string.IsNullOrWhiteSpace(turn.Chat))
            {
                throw new InvalidDataException($"{source}: turn {i + 1} needs a chat and a message.");
            }

            foreach (var expectation in turn.Expect ?? [])
            {
                if (!Types.Contains(expectation.Type))
                {
                    throw new InvalidDataException($"{source}: turn {i + 1} has an unknown expectation type '{expectation.Type}'.");
                }

                if (expectation.Type == "judge" && (string.IsNullOrWhiteSpace(expectation.Question) || expectation.PassIf is not ("yes" or "no")))
                {
                    throw new InvalidDataException($"{source}: turn {i + 1} has a judge expectation without a question or with pass_if other than yes or no.");
                }

                if (expectation.Type == "host_unchanged" && string.IsNullOrWhiteSpace(expectation.Get))
                {
                    throw new InvalidDataException($"{source}: turn {i + 1} has a host_unchanged expectation without get.");
                }
            }
        }

        return scenario with { Turns = scenario.Turns.Select(t => t with { Expect = t.Expect ?? [] }).ToList() };
    }

    public static IReadOnlyList<Scenario> LoadAll(string directory, string? pattern = null) =>
        Directory.GetFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => Parse(File.ReadAllText(path), Path.GetFileName(path)))
            .Where(s => pattern is null || System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, s.Id))
            .ToList();
}
