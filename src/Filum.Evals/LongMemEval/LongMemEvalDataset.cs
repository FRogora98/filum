using System.Text.Json;

namespace Filum.Evals.LongMemEval;

internal static class LongMemEvalDataset
{
    public static async Task<IReadOnlyList<LongMemEvalInstance>> LoadAsync(string path, Func<LongMemEvalInstance, bool>? keep = null, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await LoadAsync(stream, keep, cancellationToken);
    }

    public static async Task<IReadOnlyList<LongMemEvalInstance>> LoadAsync(Stream stream, Func<LongMemEvalInstance, bool>? keep = null, CancellationToken cancellationToken = default)
    {
        var instances = new List<LongMemEvalInstance>();
        await foreach (var instance in JsonSerializer.DeserializeAsyncEnumerable<LongMemEvalInstance>(stream, cancellationToken: cancellationToken))
        {
            if (instance is not null && (keep is null || keep(instance)))
            {
                instances.Add(instance);
            }
        }

        return instances;
    }
}
