using System.Diagnostics;
using System.Text.Json;
using Filum.Embeddings;
using Filum.Evals.LongMemEval;

namespace Filum.Evals.Cli;

// No model, no cost: the memories evals/bench/ answers LongMemEval from (spec 043).
internal static class PreloadCommand
{
    public const string Help = """
        Writes each LongMemEval question's history into its own Filum memory folder (spec 043).

          dotnet run --project src/Filum.Evals -- preload --data <longmemeval_s_cleaned.json> --ids <ids.json> --work <dir> [--embeddings <folder>]

        For every id, <work>/<id>/home holds the history as events: each session a conversation, each turn an event dated by
        its session. With --embeddings (the multilingual-e5-small folder, downloaded there when missing), the search vectors
        are made too. A folder that is already there is written again.
        """;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Has(OptionNames.Help) || !arguments.TryGet(OptionNames.Data, out var data)
            || !arguments.TryGet(OptionNames.Ids, out var idsFile) || !arguments.TryGet(OptionNames.Work, out var work))
        {
            Console.WriteLine(Help);
            return arguments.Has(OptionNames.Help) ? EvalsConstants.ExitCodes.Success : EvalsConstants.ExitCodes.CannotStart;
        }

        var wanted = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(idsFile, cancellationToken))!.ToHashSet(StringComparer.Ordinal);
        // The model is downloaded when its folder lacks it, as filum-mcp would on its first start.
        if (arguments.TryGet(OptionNames.Embeddings, out var model) && !ModelFiles.Present(model))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            if (!await ModelFiles.EnsureAsync(model, http, cancellationToken))
            {
                Console.Error.WriteLine($"The search model in {model} did not match its hash.");
                return EvalsConstants.ExitCodes.CannotStart;
            }
        }

        using var embedder = model is null ? null : new E5Embedder(model);
        var instances = await LongMemEvalDataset.LoadAsync(data, i => wanted.Contains(i.QuestionId), cancellationToken);
        var done = 0;
        foreach (var instance in instances)
        {
            var clock = Stopwatch.StartNew();
            var events = await LongMemEvalPreload.WriteAsync(instance, Path.Combine(work, instance.QuestionId, "home"), embedder, cancellationToken);
            Console.WriteLine($"[{++done}/{wanted.Count}] {instance.QuestionId}: {instance.HaystackSessions.Count} sessions, {events} events, {clock.Elapsed.TotalSeconds:0.0}s");
        }

        return EvalsConstants.ExitCodes.Success;
    }
}
