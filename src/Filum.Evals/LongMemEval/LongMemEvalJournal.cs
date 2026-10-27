using System.Text.Json;

namespace Filum.Evals.LongMemEval;

// One JSON line per judged answer, appended at once, so a stopped run can resume.
internal static class LongMemEvalJournal
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<LongMemEvalResult> Read(string path) =>
        File.Exists(path)
            ? File.ReadLines(path).Where(l => l.Trim().Length > 0).Select(l => JsonSerializer.Deserialize<LongMemEvalResult>(l, Json)).OfType<LongMemEvalResult>().ToList()
            : [];

    public static Task AppendAsync(string path, LongMemEvalResult result, CancellationToken cancellationToken) =>
        File.AppendAllTextAsync(path, JsonSerializer.Serialize(result, Json) + "\n", cancellationToken);
}
