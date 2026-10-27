using Filum.Evals.Hosts;
using Filum.Evals.Hosts.Wire;
using Filum.Evals.Judging;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;
using System.Net.Http.Json;

namespace Filum.Evals.Cli;

internal sealed class ScenarioCommand(DotEnvSecrets secrets, TimeProvider time)
{
    public const string Help = """
        Filum evals: plays scenarios against a running service that hosts the engine and compares models.

          dotnet run --project src/Filum.Evals -- --models <ids|host> [options]
          dotnet run --project src/Filum.Evals -- mcp [options]     (filum-mcp in a real agent, with and without it)
          dotnet run --project src/Filum.Evals -- longmemeval [options]   (the LongMemEval benchmark, spec 020)

          --models <ids>         catalog ids, comma-separated (see the host's models group); "host" for the host's own model
          --service <url>        the host (required)
          --prefix <path>        where the host mapped the groups (default: /api)
          --register <path>      register a new synthetic account at this path of the host, per run
          --person-header <name> instead of registering: a new person id in this header, per run
          --setup "<command>"    the host's own command after the person is made; {email} and {person} are replaced
          --scenarios-dir <dir>  the scenario folder (default: the synthetic scenarios next to the runner)
          --scenarios <glob>     scenario ids to run, for example "rule-*" (default: all)
          --reps <n>             repetitions of every scenario for every model (default: 3)
          --cap <usd>            spending cap for the run, judge included (default: 2)
          --force                start even if the estimate is above the cap (the cap still stops the run)
          --out <dir>            where results go (default: evals/results)

        The judge is gpt-5.4-mini, called with OPENAI_API_KEY from the environment or the nearest .env file.
        """;

    private const int DefaultRepetitions = 3;
    private const decimal DefaultCapUsd = 2m;

    private static readonly TimeSpan HostTimeout = TimeSpan.FromMinutes(5);

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Has(OptionNames.Help) || !arguments.TryGet(OptionNames.Models, out var modelList))
        {
            Console.WriteLine(Help);
            return arguments.Has(OptionNames.Help) ? 0 : 1;
        }

        var models = CommandLineArguments.Split(modelList);
        var repetitions = arguments.Int(OptionNames.Reps, DefaultRepetitions);
        var cap = arguments.Decimal(OptionNames.Cap, DefaultCapUsd);
        var target = SharedOptions.Target(arguments);
        var scenarios = SharedOptions.Scenarios(arguments);
        using var service = SharedOptions.HostClient(arguments, HostTimeout);
        var (known, estimate) = await EstimateAsync(service, target, models, scenarios, repetitions, cancellationToken);
        if (!known)
        {
            return EvalsConstants.ExitCodes.CannotStart;
        }

        Console.WriteLine($"{scenarios.Count} scenarios × {models.Count} models × {repetitions} repetitions · {(estimate is { } e ? $"estimated ${e:0.00}" : "no estimate for the host's own model")} · cap ${cap:0.00}");
        if (estimate > cap && !arguments.Has(OptionNames.Force))
        {
            Console.Error.WriteLine("The estimate is above the cap: lower --reps, pick fewer models or scenarios, or add --force (the cap still stops the run).");
            return EvalsConstants.ExitCodes.OverCap;
        }

        var result = await new EvalRun(new HostScenarioRunnerStrategy(service, CommandJudge.Create(secrets), target), cap, Console.Out, target.Describe(service.BaseAddress))
            .RunAsync(scenarios, models, repetitions, cancellationToken);
        var folder = await EvalReport.WriteAsync(result, arguments.OutDirectory(), time, cancellationToken);
        CommandOutput.Report(EvalReport.Markdown(result, time), folder);
        return EvalsConstants.ExitCodes.Success;
    }

    // None for the host's own model; not known when a model is not in the catalog, which is said on the error output.
    private static async Task<(bool Known, decimal? Estimate)> EstimateAsync(HttpClient service, HostTarget target, IReadOnlyList<string> models, IReadOnlyList<Scenario> scenarios, int repetitions, CancellationToken cancellationToken)
    {
        if (models.SequenceEqual([HostTarget.HostModel]))
        {
            return (true, null);
        }

        var catalog = await service.GetFromJsonAsync<List<ModelDto>>(target.Url(EvalsConstants.HostRoutes.Models), cancellationToken) ?? [];
        var unknown = models.Where(m => catalog.All(c => c.Id != m)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"Not in the host's catalog (or its provider has no key): {string.Join(", ", unknown)}. Available: {string.Join(", ", catalog.Select(c => c.Id))}");
            return (false, null);
        }

        return (true, CostEstimate.Of(scenarios, catalog.Where(c => models.Contains(c.Id)).ToList(), repetitions, JudgeModel.Price));
    }
}
