using Filum.Evals.Hosts;
using Filum.Evals.LongMemEval;

namespace Filum.Evals.Cli;

// Run by hand: never part of dotnet test or CI.
internal sealed class LongMemEvalCommand(DotEnvSecrets secrets, TimeProvider time)
{
    public const string Help = """
        LongMemEval on Filum (spec 020): the same questions on Filum, its ablations, the references and large models.

          dotnet run --project src/Filum.Evals -- longmemeval --data <file> (--subset <file> | --per-ability <n> [--seed <s>] [--save-subset <file>]) [systems] [options]

        Systems (any together; each is played on every model of --models):
          --host <label>          Filum on a running host, configured for <label> (filum, filum-no-check, filum-no-revisions);
                                  with --service, --prefix, --register or --person-header, --setup, as in the scenario runs;
                                  --consolidate: a consolidation pass after each session (spec 030)
          --baselines none,naive  the references, called directly with --models-file (a host's appsettings: Models, Providers)
          --claude filum,none,full  Claude Code (the owner's subscription), with --filum-mcp <path> and --agent-model <name>;
                                  --consolidate: with filum, a session that tidies the memory after each history session;
                                  --hook: with filum, every message kept by filum-mcp log (spec 036)

        Options:
          --models <ids>          the models (catalog ids; "host" for a host's own) for --host and --baselines
          --models-file <file>    prices for the estimate and the references' providers (default: none)
          --reps <n>              repetitions (default 1)
          --cap <usd>             dollar cap, judge included (default 4); --max-sessions <n> caps agent sessions (default 0 = none)
          --force                 start even if the estimate is above the cap (the caps still stop the run)
          --out <dir>             where results go (default: evals/results)
          --journal <file>        every answer written as it is judged; answers already there are not played again (resume)
          --retrieval             the search alone: recall@5 and @10 of the sessions holding the answer, no model, no cost (spec 023);
                                  --embeddings <folder> adds the local embedding model
          --gate --embeddings <folder>  trains and tests the keep-or-discard gate on the file, no cost (spec 022); --per-class <n>

        Download the data first: scripts/longmemeval-download.sh oracle s.
        """;

    private const int DefaultPerAbility = 4;
    private const int DefaultSeed = 1;
    private const int DefaultRepetitions = 1;
    private const decimal DefaultCapUsd = 4m;

    // No cap unless one is given: a run that needs sessions then refuses to start.
    private const int DefaultMaxSessions = 0;

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.TryGet(OptionNames.Report, out var journals))
        {
            return await LongMemEvalJournalReportCommand.RunAsync(arguments, journals, time, cancellationToken);
        }

        if (arguments.Has(OptionNames.Help) || !arguments.TryGet(OptionNames.Data, out var data))
        {
            Console.WriteLine(Help);
            return arguments.Has(OptionNames.Help) ? 0 : 1;
        }

        var (subset, instances) = await LoadAsync(arguments, data, cancellationToken);
        if (arguments.Has(OptionNames.Gate))
        {
            return await LongMemEvalGateCommand.RunAsync(arguments, data, subset, instances, time, cancellationToken);
        }

        return arguments.Has(OptionNames.Retrieval)
            ? await LongMemEvalRetrievalCommand.RunAsync(arguments, data, subset, instances, time, cancellationToken)
            : await PlayAsync(arguments, data, subset, instances, cancellationToken);
    }

    // The questions in the subset's order.
    private static async Task<(LongMemEvalSubset Subset, IReadOnlyList<LongMemEvalInstance> Instances)> LoadAsync(CommandLineArguments arguments, string data, CancellationToken cancellationToken)
    {
        var subset = arguments.TryGet(OptionNames.Subset, out var subsetFile)
            ? LongMemEvalSubset.Load(subsetFile)
            : LongMemEvalSubset.Build(await LongMemEvalDataset.LoadAsync(data, cancellationToken: cancellationToken), arguments.Int(OptionNames.PerAbility, DefaultPerAbility), arguments.Int(OptionNames.Seed, DefaultSeed));
        if (arguments.TryGet(OptionNames.SaveSubset, out var saveTo))
        {
            subset.Save(saveTo);
        }

        var wanted = subset.QuestionIds.ToHashSet(StringComparer.Ordinal);
        var instances = (await LongMemEvalDataset.LoadAsync(data, i => wanted.Contains(i.QuestionId), cancellationToken)).OrderBy(i => subset.QuestionIds.ToList().IndexOf(i.QuestionId)).ToList();
        return (subset, instances);
    }

    private async Task<int> PlayAsync(CommandLineArguments arguments, string data, LongMemEvalSubset subset, IReadOnlyList<LongMemEvalInstance> instances, CancellationToken cancellationToken)
    {
        var models = CommandLineArguments.Split(arguments.Get(OptionNames.Models, HostTarget.HostModel));
        var modelsFile = arguments.Get(OptionNames.ModelsFile) is { } modelsPath ? ModelsFile.Load(modelsPath) : null;
        var factory = new LongMemEvalSystemFactory(arguments, secrets);
        var systems = factory.Create(models, modelsFile);
        using var service = factory.Service;
        if (systems.Count == 0)
        {
            Console.Error.WriteLine("Name at least one system: --host, --baselines or --claude.");
            return EvalsConstants.ExitCodes.CannotStart;
        }

        var repetitions = arguments.Int(OptionNames.Reps, DefaultRepetitions);
        var cap = arguments.Decimal(OptionNames.Cap, DefaultCapUsd);
        var maxSessions = arguments.Int(OptionNames.MaxSessions, DefaultMaxSessions);
        var sessions = systems.Sum(s => instances.Sum(s.SessionsFor)) * repetitions;
        if (!CanStart(arguments, instances.Count, systems.Count, repetitions, LongMemEvalEstimate.Of(instances, systems, modelsFile?.Prices() ?? new Dictionary<string, ModelPrice>()) * repetitions, cap, sessions, maxSessions))
        {
            return EvalsConstants.ExitCodes.OverCap;
        }

        var description = $"Data: {Path.GetFileName(data)} · subset seed {subset.Seed}, {subset.PerAbility} per ability · {string.Join(" · ", systems.Select(s => $"{s.Name} ({s.Model})").Distinct())}";
        var result = await new LongMemEvalRun(CommandJudge.Create(secrets), cap, sessions > 0 ? maxSessions : int.MaxValue, Console.Out, description, arguments.Get(OptionNames.Journal))
            .RunAsync(instances, systems, repetitions, cancellationToken);
        var folder = await LongMemEvalReport.WriteAsync(result, arguments.OutDirectory(), time, cancellationToken);
        CommandOutput.Report(LongMemEvalReport.Markdown(result, time), folder);
        return EvalsConstants.ExitCodes.Success;
    }

    // False, with the reason on the error output, when a cap refuses the run and --force is not given.
    private static bool CanStart(CommandLineArguments arguments, int questions, int systems, int repetitions, decimal estimate, decimal cap, int sessions, int maxSessions)
    {
        Console.WriteLine($"{questions} questions × {systems} systems × {repetitions} repetitions · estimated ${estimate:0.00} · cap ${cap:0.00}{(sessions > 0 ? $" · {sessions} agent sessions, cap {maxSessions}" : "")}");
        if ((estimate > cap || (sessions > 0 && sessions > maxSessions)) && !arguments.Has(OptionNames.Force))
        {
            Console.Error.WriteLine(sessions > maxSessions && sessions > 0
                ? "The run needs more agent sessions than --max-sessions."
                : "The estimate is above the cap: pick fewer questions, systems or models, or add --force (the caps still stop the run).");
            return false;
        }

        return true;
    }
}
