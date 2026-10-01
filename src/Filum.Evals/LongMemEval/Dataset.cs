using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Filum.Evals.LongMemEval;

/// <summary>One turn of a LongMemEval history session.</summary>
public sealed record LmeTurn([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

/// <summary>
/// One LongMemEval question with its history (LongMemEval, MIT, © 2024 Di Wu: https://github.com/xiaowu0162/LongMemEval).
/// The answer is a text or a number in the files, so it is kept as JSON and read as text.
/// </summary>
public sealed record LmeInstance(
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("question_type")] string QuestionType,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("answer")] JsonElement Answer,
    [property: JsonPropertyName("question_date")] string QuestionDate,
    [property: JsonPropertyName("haystack_dates")] IReadOnlyList<string> HaystackDates,
    [property: JsonPropertyName("haystack_sessions")] IReadOnlyList<IReadOnlyList<LmeTurn>> HaystackSessions)
{
    public string AnswerText => Answer.ValueKind == JsonValueKind.String ? Answer.GetString()! : Answer.GetRawText();

    public bool IsAbstention => QuestionId.EndsWith("_abs", StringComparison.Ordinal);

    /// <summary>The ability the question tests, the five of the benchmark's paper.</summary>
    public string Ability => IsAbstention ? LmeAbilities.Abstention : QuestionType switch
    {
        "single-session-user" or "single-session-assistant" or "single-session-preference" => LmeAbilities.Extraction,
        "multi-session" => LmeAbilities.MultiSession,
        "knowledge-update" => LmeAbilities.KnowledgeUpdate,
        "temporal-reasoning" => LmeAbilities.Temporal,
        _ => QuestionType
    };

    /// <summary>The sessions with their dates, in date order: the files do not keep them in that order.</summary>
    public IEnumerable<(string Date, IReadOnlyList<LmeTurn> Turns)> Sessions =>
        HaystackSessions
            .Select((turns, i) => (Date: i < HaystackDates.Count ? HaystackDates[i] : string.Empty, Turns: turns, Index: i))
            .OrderBy(s => When(s.Date))
            .ThenBy(s => s.Index)
            .Select(s => (s.Date, s.Turns));

    /// <summary>A date of the files, "2023/04/10 (Mon) 17:50"; one that does not read sorts first, in file order.</summary>
    public static DateTime When(string date)
    {
        var cut = date.IndexOf(" (", StringComparison.Ordinal);
        var time = cut > 0 && date.IndexOf(") ", cut, StringComparison.Ordinal) is var close and > 0 ? date[..cut] + " " + date[(close + 2)..] : date;
        return DateTime.TryParseExact(time, ["yyyy/MM/dd HH:mm", "yyyy/MM/dd"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var when)
            ? when
            : DateTime.MinValue;
    }

    public int HistoryCharacters => HaystackSessions.Sum(s => s.Sum(t => t.Content.Length));
}

public static class LmeAbilities
{
    public const string Extraction = "extraction";
    public const string MultiSession = "multi-session";
    public const string KnowledgeUpdate = "knowledge-update";
    public const string Temporal = "temporal";
    public const string Abstention = "abstention";

    public static IReadOnlyList<string> All { get; } = [Extraction, MultiSession, KnowledgeUpdate, Temporal, Abstention];
}

public static class LmeDataset
{
    /// <summary>Reads a LongMemEval file (a JSON array), keeping only the questions <paramref name="keep"/> accepts.</summary>
    public static async Task<IReadOnlyList<LmeInstance>> LoadAsync(string path, Func<LmeInstance, bool>? keep = null, CancellationToken cancellationToken = default)
    {
        var instances = new List<LmeInstance>();
        await using var stream = File.OpenRead(path);
        await foreach (var instance in JsonSerializer.DeserializeAsyncEnumerable<LmeInstance>(stream, cancellationToken: cancellationToken))
        {
            if (instance is not null && (keep is null || keep(instance)))
            {
                instances.Add(instance);
            }
        }

        return instances;
    }
}

/// <summary>A fixed, stratified set of question ids: the same number per ability, chosen by a seed.</summary>
public sealed record LmeSubset(int Seed, int PerAbility, IReadOnlyList<string> QuestionIds)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Per ability, the questions ordered by a hash of the seed and their id, the first <paramref name="perAbility"/>
    /// taken: the same seed always gives the same ids, on any machine and any version of .NET.
    /// </summary>
    public static LmeSubset Build(IEnumerable<LmeInstance> instances, int perAbility, int seed)
    {
        var ids = instances
            .GroupBy(i => i.Ability)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(i => Rank(seed, i.QuestionId), StringComparer.Ordinal).Take(perAbility))
            .Select(i => i.QuestionId)
            .ToList();
        return new LmeSubset(seed, perAbility, ids);
    }

    public static LmeSubset Load(string path) => JsonSerializer.Deserialize<LmeSubset>(File.ReadAllText(path), Json)!;

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json) + "\n");

    private static string Rank(int seed, string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{id}")));
}
