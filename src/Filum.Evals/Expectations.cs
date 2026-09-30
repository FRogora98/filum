using System.Text.Json;
using System.Text.RegularExpressions;

namespace Filum.Evals;

/// <summary>What one turn produced, as the service reported it.</summary>
public sealed record TurnRecord(
    int Index,
    string Chat,
    string Message,
    string Answer,
    IReadOnlyList<StepDto> Steps,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    double LatencySeconds,
    string? Error = null,
    SkillProposalDto? Proposal = null,
    int? Status = null,
    bool ConversationExists = true,
    IReadOnlyDictionary<string, bool>? Unchanged = null)
{
    /// <summary>The host refused the turn (a status in the 4xx range), as its turn gate does.</summary>
    public bool Refused => Status is >= 400 and < 500;

    public bool Wrote => Steps.Any(s => s.Kind == StepDto.Wrote);

    public bool UsedSkill => Steps.Any(s => s.Kind == StepDto.Read && s.Description.StartsWith("Used skill ", StringComparison.Ordinal));
}

/// <summary>The person's whole memory after a turn, read through the public endpoints.</summary>
/// <param name="Available">False when the host maps no memory group: then the memory expectations cannot be checked.</param>
public sealed record MemorySnapshot(IReadOnlyList<MemoryFileDetailDto> Files, bool Available = true)
{
    public static MemorySnapshot Unavailable { get; } = new([], Available: false);

    public string Core => Files.FirstOrDefault(f => f.Path == "/filum.md")?.Content ?? string.Empty;

    /// <summary>The person's skills but the starter ones, parsed; a skill file that does not parse is left out.</summary>
    public IReadOnlyList<(string Path, Skill Skill)> OwnSkills =>
        Files.Where(f => Skills.IsSkillPath(f.Path))
            .Select(f => (f.Path, Skill: Skills.Parse(f.Content).Skill))
            .Where(s => s.Skill is not null && !Skills.Starters.Any(starter => starter.Name == s.Skill.Name))
            .Select(s => (s.Path, s.Skill!))
            .ToList();
}

public sealed record ExpectationResult(Expectation Expectation, bool Passed, string Detail);

/// <summary>Every expectation is code: no model decides whether a turn did what the scenario asks.</summary>
public static partial class Expectations
{
    /// <param name="earlier">The turns of the scenario before this one, for expectations about the whole run so far.</param>
    public static ExpectationResult Check(Expectation e, TurnRecord turn, MemorySnapshot memory, IReadOnlyList<TurnRecord>? earlier = null)
    {
        if (e.Type == "refused")
        {
            // The one expectation for which a turn that did not answer is the point.
            return CheckRefused(e, turn);
        }

        if (!memory.Available && ScenarioLoader.MemoryTypes.Contains(e.Type))
        {
            return new ExpectationResult(e, false, "the host maps no memory group");
        }

        var (passed, detail) = e.Type switch
        {
            "wrote" => (turn.Wrote, turn.Wrote ? "a change was saved" : "no change was saved"),
            "no_write" => (!turn.Wrote, turn.Wrote ? "a change was saved" : "nothing was changed"),
            "step" => Has(turn.Steps.Any(s => s.Kind == e.Kind), $"steps: {string.Join(", ", turn.Steps.Select(s => s.Kind))}"),
            "answer_contains" => Has(ContainsAny(turn.Answer, e.Any), Quote(turn.Answer)),
            "answer_not_contains" => Has(!ContainsAny(turn.Answer, e.Any), Quote(turn.Answer)),
            "answer_max_words" => (Words(turn.Answer) <= (e.Max ?? int.MaxValue), $"{Words(turn.Answer)} words"),
            "core_contains" => Has(ContainsAny(memory.Core, e.Any), Quote(memory.Core)),
            "core_not_contains" => Has(!ContainsAny(memory.Core, e.Any), Quote(memory.Core)),
            "memory_contains" => Has(memory.Files.Any(f => ContainsAny(f.Content, e.Any)), $"{memory.Files.Count} files"),
            "file" => CheckFile(e, memory),
            "rows" => CheckRows(e, memory),
            "skill" => CheckSkill(e, memory),
            "no_skill" => Has(!SkillsNamed(e, memory).Any(), $"skills: {SkillNames(memory)}"),
            "used_skill" => Has(turn.UsedSkill, $"steps: {string.Join(", ", turn.Steps.Select(s => s.Description))}"),
            "proposal" => CheckProposal(e, [.. earlier ?? [], turn]),
            "tool" => Has(turn.Steps.Any(s => e.Any.Contains(s.Tool)), $"tools: {Tools(turn)}"),
            "no_tool" => Has(!turn.Steps.Any(s => e.Any.Contains(s.Tool)), $"tools: {Tools(turn)}"),
            "surfaced" => CheckSurfaced(e, turn),
            "host_unchanged" => turn.Unchanged?.TryGetValue(e.Get!, out var same) == true
                ? Has(same, same ? $"{e.Get} is the same" : $"{e.Get} changed")
                : (false, $"{e.Get} could not be read"),
            "judge" => (false, "the judge's expectations are checked by the runner"),
            _ => (false, $"unknown expectation type '{e.Type}'")
        };

        if (turn.Error is not null)
        {
            passed = false;
            detail = $"the turn failed: {turn.Error}";
        }

        return new ExpectationResult(e, passed, detail);
    }

    private static (bool, string) Has(bool ok, string detail) => (ok, detail);

    private static string Tools(TurnRecord turn) => turn.Steps.Count == 0 ? "none" : string.Join(", ", turn.Steps.Select(s => s.Tool));

    private static ExpectationResult CheckRefused(Expectation e, TurnRecord turn)
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

    /// <summary>A step of one of the tools with surfaced data: its fields there and not empty, its values equal, its texts in it.</summary>
    private static (bool, string) CheckSurfaced(Expectation e, TurnRecord turn)
    {
        var surfaced = turn.Steps.Where(s => (e.Any.Count == 0 || e.Any.Contains(s.Tool)) && s.Data is not null).ToList();
        if (surfaced.Count == 0)
        {
            return (false, $"no surfaced data; tools: {Tools(turn)}");
        }

        var problems = new List<string>();
        foreach (var step in surfaced)
        {
            var data = step.Data!.Value;
            problems.Clear();
            problems.AddRange(e.Fields.Where(f => !HasValue(data, f)).Select(f => $"no {f}"));
            problems.AddRange(e.EqualTo.Where(kv => !HasValue(data, kv.Key) || !JsonElement.DeepEquals(data.GetProperty(kv.Key), kv.Value)).Select(kv => $"{kv.Key} is not {kv.Value}"));
            problems.AddRange(e.Contain.Where(text => !Normalize(data.GetRawText()).Contains(Normalize(text), StringComparison.Ordinal)).Select(text => $"no \"{text}\""));
            if (problems.Count == 0)
            {
                return (true, $"{step.Tool}: {Short(data.GetRawText())}");
            }
        }

        return (false, $"{surfaced[^1].Tool}: {string.Join(", ", problems)} in {Short(surfaced[^1].Data!.Value.GetRawText())}");
    }

    private static bool HasValue(JsonElement data, string field) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(field, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
        && !(value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static string Short(string text) => text.Length <= 120 ? text : text[..117] + "…";

    private static (bool, string) CheckFile(Expectation e, MemorySnapshot memory)
    {
        var candidates = Named(e, memory).Where(f => e.Kind is null || f.Kind == e.Kind).ToList();
        if (candidates.Count == 0)
        {
            return (false, $"no {e.Kind ?? "file"} named {string.Join("|", e.NameAny)} among {Names(memory)}");
        }

        return e.Sensitivity is null || candidates.Any(f => f.Sensitivity == e.Sensitivity)
            ? (true, candidates[0].Path)
            : (false, $"{candidates[0].Path} is {candidates[0].Sensitivity}");
    }

    private static (bool, string) CheckRows(Expectation e, MemorySnapshot memory)
    {
        var collection = Named(e, memory).FirstOrDefault(f => f.Kind == "collection");
        if (collection is null)
        {
            return (false, $"no collection named {string.Join("|", e.NameAny)} among {Names(memory)}");
        }

        var table = Csv.Check(collection.Content);
        var rows = table.Rows.Select(r => string.Join(" | ", r.Fields)).ToList();
        if (e.Count is { } count && rows.Count != count)
        {
            return (false, $"{collection.Path} has {rows.Count} rows, expected {count}");
        }

        var missing = e.Contain.Where(value => !rows.Any(row => Normalize(row).Contains(Normalize(value), StringComparison.Ordinal))).ToList();
        return missing.Count == 0
            ? (true, $"{collection.Path}: {rows.Count} rows")
            : (false, $"{collection.Path} has no row with {string.Join(", ", missing)}");
    }

    private static (bool, string) CheckSkill(Expectation e, MemorySnapshot memory)
    {
        var skills = SkillsNamed(e, memory).ToList();
        if (skills.Count == 0)
        {
            return (false, $"no skill named {string.Join("|", e.NameAny)} among {SkillNames(memory)}");
        }

        return e.Enabled is null || skills.Any(s => s.Skill.Enabled == e.Enabled)
            ? (true, $"/{skills[0].Skill.Name}{(skills[0].Skill.Enabled ? "" : " (off)")}")
            : (false, $"/{skills[0].Skill.Name} is {(skills[0].Skill.Enabled ? "on" : "off")}");
    }

    /// <summary>A proposal in this turn or an earlier one: the model decides when a request has repeated enough.</summary>
    private static (bool, string) CheckProposal(Expectation e, IReadOnlyList<TurnRecord> turns)
    {
        var proposals = turns.Select(t => t.Proposal).OfType<SkillProposalDto>().ToList();
        var named = proposals.Where(p => e.NameAny.Count == 0 || e.NameAny.Any(word => Normalize(p.Name).Contains(Normalize(word), StringComparison.Ordinal))).ToList();
        return named.Count > 0
            ? (true, $"proposed /{named[^1].Name}")
            : (false, proposals.Count == 0 ? "no proposal" : $"proposed {string.Join(", ", proposals.Select(p => "/" + p.Name))}");
    }

    /// <summary>The person's own skills (not the starter ones) whose name has one of the words; any of them when none is given.</summary>
    private static IEnumerable<(string Path, Skill Skill)> SkillsNamed(Expectation e, MemorySnapshot memory) =>
        memory.OwnSkills.Where(s => e.NameAny.Count == 0 || e.NameAny.Any(word => Normalize(s.Skill.Name).Contains(Normalize(word), StringComparison.Ordinal)));

    private static string SkillNames(MemorySnapshot memory) =>
        memory.OwnSkills.Count == 0 ? "(only the starter skills)" : string.Join(", ", memory.OwnSkills.Select(s => "/" + s.Skill.Name));

    private static IEnumerable<MemoryFileDetailDto> Named(Expectation e, MemorySnapshot memory) =>
        // The model picks names and folders ("/shopping/list.csv", "/films-to-watch.csv"): any word anywhere in the path counts.
        // Skill files are not the person's data: they are checked by the skill expectations.
        memory.Files.Where(f => f.Path != "/filum.md" && !Skills.IsSkillPath(f.Path) && (e.NameAny.Count == 0 || e.NameAny.Any(word => Normalize(f.Path).Contains(Normalize(word), StringComparison.Ordinal))));

    private static string Names(MemorySnapshot memory)
    {
        var names = memory.Files.Where(f => f.Path != "/filum.md" && !Skills.IsSkillPath(f.Path)).Select(f => f.Path).ToList();
        return names.Count == 0 ? "(no files but the core and the skills)" : string.Join(", ", names);
    }

    public static bool ContainsAny(string text, IReadOnlyList<string> values)
    {
        var haystack = Normalize(text);
        return values.Any(v => haystack.Contains(Normalize(v), StringComparison.Ordinal));
    }

    /// <summary>
    /// Lower case, typographic quotes as plain ones ("don’t" reads "don't"), and numbers without thousand separators:
    /// "2,396", "2.396" and "2 396" all read "2396".
    /// </summary>
    public static string Normalize(string text) =>
        ThousandSeparator().Replace(text.ToLowerInvariant().Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"'), string.Empty);

    public static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Quote(string text) => text.Length <= 160 ? $"\"{text.ReplaceLineEndings(" ")}\"" : $"\"{text[..157].ReplaceLineEndings(" ")}…\"";

    [GeneratedRegex(@"(?<=\d)[.,'   ](?=\d{3}(?!\d))")]
    private static partial Regex ThousandSeparator();
}
