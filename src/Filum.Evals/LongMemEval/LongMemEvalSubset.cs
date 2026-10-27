using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Filum.Evals.LongMemEval;

internal sealed record LongMemEvalSubset(int Seed, int PerAbility, IReadOnlyList<string> QuestionIds)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Ordered by a hash of the seed and the id: the same seed always gives the same ids, on any machine and any version
    // of .NET.
    public static LongMemEvalSubset Build(IEnumerable<LongMemEvalInstance> instances, int perAbility, int seed)
    {
        var ids = instances
            .GroupBy(i => i.Ability)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(i => Rank(seed, i.QuestionId), StringComparer.Ordinal).Take(perAbility))
            .Select(i => i.QuestionId)
            .ToList();
        return new LongMemEvalSubset(seed, perAbility, ids);
    }

    public static LongMemEvalSubset Load(string path) =>
        JsonSerializer.Deserialize<LongMemEvalSubset>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path} does not hold a subset.");

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json) + "\n");

    private static string Rank(int seed, string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{id}")));
}
