using System.Text.Json;
using System.Text.Json.Serialization;

namespace Filum.Evals.Scenarios;

// Which fields matter depends on Type.
internal sealed record Expectation
{
    public string Type { get; init; } = string.Empty;

    // One of them must (or must not) be found.
    public IReadOnlyList<string> Any { get; init; } = [];

    // One of them must be in the file's name: the model picks its own names.
    [JsonPropertyName("name_any")]
    public IReadOnlyList<string> NameAny { get; init; } = [];

    // collection or document for file; a step kind for step.
    public string? Kind { get; init; }

    public string? Sensitivity { get; init; }

    // For rows.
    public int? Count { get; init; }

    // For rows: each must appear in some row.
    public IReadOnlyList<string> Contain { get; init; } = [];

    // For answer_max_words.
    public int? Max { get; init; }

    // For skill.
    public bool? Enabled { get; init; }

    // Counts in "rules kept".
    public bool Rule { get; init; }

    // For surfaced: fields that must be there, not empty.
    public IReadOnlyList<string> Fields { get; init; } = [];

    // For surfaced: field → value.
    [JsonPropertyName("equals")]
    public IReadOnlyDictionary<string, JsonElement> EqualTo { get; init; } = new Dictionary<string, JsonElement>();

    // For host_unchanged: read before and after the turn.
    public string? Get { get; init; }

    // For judge: the scenario's own yes/no question.
    public string? Question { get; init; }

    // For judge: the answer that passes, yes or no.
    [JsonPropertyName("pass_if")]
    public string? PassIf { get; init; }

    public override string ToString() => Type switch
    {
        ExpectationTypes.File => $"file {Kind} named {Alternatives(NameAny)}{(Sensitivity is null ? "" : $" ({Sensitivity})")}",
        ExpectationTypes.Rows => $"rows of {Alternatives(NameAny)}{(Count is null ? "" : $" = {Count}")}{Containing()}",
        ExpectationTypes.Step => $"a {Kind} step",
        ExpectationTypes.AnswerMaxWords => $"answer at most {Max} words",
        ExpectationTypes.Skill => $"skill named {Alternatives(NameAny)}{(Enabled is null ? "" : Enabled.Value ? " (on)" : " (off)")}",
        ExpectationTypes.NoSkill => NameAny.Count == 0 ? "no skill saved" : $"no skill named {Alternatives(NameAny)}",
        ExpectationTypes.Proposal => NameAny.Count == 0 ? "a skill proposed" : $"a skill proposed named {Alternatives(NameAny)}",
        ExpectationTypes.Wrote or ExpectationTypes.NoWrite or ExpectationTypes.UsedSkill => Type,
        ExpectationTypes.Tool => $"a call of {Alternatives(Any)}",
        ExpectationTypes.NoTool => $"no call of {Alternatives(Any)}",
        ExpectationTypes.Surfaced => $"surfaced data from {Alternatives(Any)}{(Fields.Count == 0 ? "" : $" with {string.Join(", ", Fields)}")}{(EqualTo.Count == 0 ? "" : $" where {string.Join(", ", EqualTo.Select(kv => $"{kv.Key} = {kv.Value}"))}")}{Containing()}",
        ExpectationTypes.Refused => Count is null ? "the turn refused" : $"the turn refused with {Count}",
        ExpectationTypes.HostUnchanged => $"{Get} unchanged",
        ExpectationTypes.Judge => $"the judge says {PassIf} to \"{Question}\"",
        _ => $"{Type} {Alternatives(Any)}"
    };

    private static string Alternatives(IReadOnlyList<string> values) => string.Join("|", values);

    private string Containing() => Contain.Count == 0 ? "" : $" containing {string.Join(", ", Contain)}";
}
