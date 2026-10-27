using Filum.Embeddings;
using Filum.Evals.LongMemEval;
using Filum.Evals.LongMemEval.Analysis;
using System.Globalization;
using System.Text.Json;

namespace Filum.Evals.Cli;

// No model, no cost.
internal static class LongMemEvalRetrievalCommand
{
    public static async Task<int> RunAsync(CommandLineArguments arguments, string data, LongMemEvalSubset subset, IReadOnlyList<LongMemEvalInstance> instances, TimeProvider time, CancellationToken cancellationToken)
    {
        using var embedder = arguments.TryGet(OptionNames.Embeddings, out var modelFolder) ? new E5Embedder(modelFolder) : null;
        var retrieval = await LongMemEvalRetrieval.RunAsync(instances, () => new InMemoryMemoryStore(), Console.Out, time, cancellationToken, embedder);
        var folder = Path.Combine(arguments.OutDirectory(), $"retrieval-{time.GetUtcNow().ToString(ResultsFolder.TimeFormat, CultureInfo.InvariantCulture)}");
        var ranking = embedder is null ? " (words)" : " (words and meaning, multilingual-e5-small)";
        var markdown = LongMemEvalRetrieval.Markdown(retrieval, arguments.Get(OptionNames.Describe,
            $"Data: {Path.GetFileName(data)} · subset seed {subset.Seed}, {subset.PerAbility} per ability · events_search's ranking{ranking}, top {LongMemEvalRetrieval.Hits}."), time);
        await ResultsFolder.WriteAsync(folder, [(ResultsFolder.ReportFile, markdown), (ResultsFolder.ResultsFile, JsonSerializer.Serialize(retrieval, JsonDefaults.Indented))], cancellationToken);
        Console.WriteLine(markdown);
        CommandOutput.Written(folder);
        return EvalsConstants.ExitCodes.Success;
    }
}
