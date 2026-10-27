using System.IO.Enumeration;
using System.Text.Json;

namespace Filum.Evals.Scenarios;

// A file the runner could not play is refused before any turn is played.
internal static class ScenarioLoader
{
    private const string ScenarioFiles = "*.json";

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
            Validate(scenario.Turns[i], $"{source}: turn {i + 1}");
        }

        return scenario with { Turns = scenario.Turns.Select(t => t with { Expect = t.Expect ?? [] }).ToList() };
    }

    public static IReadOnlyList<Scenario> LoadAll(string directory, string? pattern = null) =>
        Directory.GetFiles(directory, ScenarioFiles)
            .Order(StringComparer.Ordinal)
            .Select(path => Parse(File.ReadAllText(path), Path.GetFileName(path)))
            .Where(s => pattern is null || FileSystemName.MatchesSimpleExpression(pattern, s.Id))
            .ToList();

    private static void Validate(ScenarioTurn turn, string where)
    {
        if (string.IsNullOrWhiteSpace(turn.Message) || string.IsNullOrWhiteSpace(turn.Chat))
        {
            throw new InvalidDataException($"{where} needs a chat and a message.");
        }

        foreach (var expectation in turn.Expect ?? [])
        {
            Validate(expectation, where);
        }
    }

    private static void Validate(Expectation expectation, string where)
    {
        if (!ExpectationTypes.All.Contains(expectation.Type))
        {
            throw new InvalidDataException($"{where} has an unknown expectation type '{expectation.Type}'.");
        }

        if (expectation.Type == ExpectationTypes.Judge && (string.IsNullOrWhiteSpace(expectation.Question) || expectation.PassIf is not (ExpectationTypes.Yes or ExpectationTypes.No)))
        {
            throw new InvalidDataException($"{where} has a judge expectation without a question or with pass_if other than yes or no.");
        }

        if (expectation.Type == ExpectationTypes.HostUnchanged && string.IsNullOrWhiteSpace(expectation.Get))
        {
            throw new InvalidDataException($"{where} has a host_unchanged expectation without get.");
        }
    }
}
