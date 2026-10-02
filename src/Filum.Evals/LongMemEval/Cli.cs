using System.Globalization;
using System.Net.Http.Json;

namespace Filum.Evals.LongMemEval;

/// <summary>The LongMemEval benchmark (spec 020), run by hand: never part of dotnet test or CI.</summary>
public static class LmeCli
{
    public const string Help = """
        LongMemEval on Filum (spec 020): the same questions on Filum, its ablations, the references and large models.

          dotnet run --project src/Filum.Evals -- longmemeval --data <file> (--subset <file> | --per-ability <n> [--seed <s>] [--save-subset <file>]) [systems] [options]

        Systems (any together; each is played on every model of --models):
          --host <label>          Filum on a running host, configured for <label> (filum, filum-no-check, filum-no-revisions);
                                  with --service, --prefix, --register or --person-header, --setup, as in the scenario runs;
                                  --consolidate: a consolidation pass after each session (spec 030)
          --baselines none,naive  the references, called directly with --models-file (a host's appsettings: Models, Providers)
          --claude filum,none,full  Claude Code (the owner's subscription), with --filum-mcp <path> and --agent-model <name>

        Options:
          --models <ids>          the models (catalog ids; "host" for a host's own) for --host and --baselines
          --models-file <file>    prices for the estimate and the references' providers (default: none)
          --reps <n>              repetitions (default 1)
          --cap <usd>             dollar cap, judge included (default 4); --max-sessions <n> caps agent sessions (default 0 = none)
          --force                 start even if the estimate is above the cap (the caps still stop the run)
          --out <dir>             where results go (default: evals/results)
          --journal <file>        every answer written as it is judged; answers already there are not played again (resume)

        Download the data first: scripts/longmemeval-download.sh oracle s.
        """;

    public static async Task<int> MainAsync(string[] args)
    {
        var options = Args.Parse(args);
        if (options.TryGetValue("report", out var journals))
        {
            // Report only: the answers of one or more journals, comma-separated, as one report; nothing is played.
            var all = journals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SelectMany(LmeRun.ReadJournal).ToList();
            var merged = new LmeRunResult(all, all.Sum(r => r.CostUsd + r.JudgeCostUsd), false, all.Count, 0, options.GetValueOrDefault("describe", string.Empty));
            var written = await LmeReport.WriteAsync(merged, options.GetValueOrDefault("out", Path.Combine("evals", "results")));
            Console.WriteLine(LmeReport.Markdown(merged));
            Console.WriteLine($"Written to {Path.GetFullPath(written)}");
            return 0;
        }

        if (options.ContainsKey("help") || !options.TryGetValue("data", out var data))
        {
            Console.WriteLine(Help);
            return options.ContainsKey("help") ? 0 : 1;
        }

        var subset = options.TryGetValue("subset", out var subsetFile)
            ? LmeSubset.Load(subsetFile)
            : LmeSubset.Build(await LmeDataset.LoadAsync(data), int.Parse(options.GetValueOrDefault("per-ability", "4"), CultureInfo.InvariantCulture), int.Parse(options.GetValueOrDefault("seed", "1"), CultureInfo.InvariantCulture));
        if (options.TryGetValue("save-subset", out var saveTo))
        {
            subset.Save(saveTo);
        }

        var wanted = subset.QuestionIds.ToHashSet(StringComparer.Ordinal);
        var instances = (await LmeDataset.LoadAsync(data, i => wanted.Contains(i.QuestionId))).OrderBy(i => subset.QuestionIds.ToList().IndexOf(i.QuestionId)).ToList();
        var models = options.GetValueOrDefault("models", HostTarget.HostModel).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modelsFile = options.GetValueOrDefault("models-file");
        var systems = new List<ILmeSystem>();
        HttpClient? service = null;

        if (options.TryGetValue("host", out var label))
        {
            service = new HttpClient { BaseAddress = new Uri(options.GetValueOrDefault("service", "http://localhost:5410")), Timeout = TimeSpan.FromMinutes(10) };
            var target = new HostTarget(options.GetValueOrDefault("prefix", "/api"), options.GetValueOrDefault("register", "/api/auth/register"), options.GetValueOrDefault("person-header"), options.GetValueOrDefault("setup"));
            systems.AddRange(models.Select(m => new HostLmeSystem(service, target, m, label, options.ContainsKey("consolidate"))));
        }

        foreach (var baseline in options.GetValueOrDefault("baselines", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var file = modelsFile ?? throw new InvalidOperationException("--baselines needs --models-file.");
            systems.AddRange(models.Select(m => baseline switch
            {
                "none" => (ILmeSystem)new NoMemorySystem(DirectModel.Load(file, m)),
                "naive" => new NaiveRetrievalSystem(DirectModel.Load(file, m)),
                _ => throw new InvalidOperationException($"Unknown baseline '{baseline}': none or naive.")
            }));
        }

        foreach (var mode in options.GetValueOrDefault("claude", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var agent = new ClaudeCodeAgent(options.GetValueOrDefault("agent-model"));
            var filumMcp = Path.GetFullPath(options.GetValueOrDefault("filum-mcp") ?? throw new InvalidOperationException("--claude needs --filum-mcp <path>."));
            systems.Add(new ClaudeLmeSystem(agent, mode switch
            {
                "filum" => ClaudeLmeSystem.Mode.WithFilum,
                "none" => ClaudeLmeSystem.Mode.NoMemory,
                "full" => ClaudeLmeSystem.Mode.FullContext,
                _ => throw new InvalidOperationException($"Unknown Claude mode '{mode}': filum, none or full.")
            }, filumMcp, agent.Model ?? "claude"));
        }

        if (systems.Count == 0)
        {
            Console.Error.WriteLine("Name at least one system: --host, --baselines or --claude.");
            return 1;
        }

        var repetitions = int.Parse(options.GetValueOrDefault("reps", "1"), CultureInfo.InvariantCulture);
        var cap = decimal.Parse(options.GetValueOrDefault("cap", "4"), CultureInfo.InvariantCulture);
        var maxSessions = int.Parse(options.GetValueOrDefault("max-sessions", "0"), CultureInfo.InvariantCulture);
        var sessions = systems.Sum(s => instances.Sum(s.SessionsFor)) * repetitions;
        var estimate = Estimate(instances, systems, modelsFile) * repetitions;
        Console.WriteLine($"{instances.Count} questions × {systems.Count} systems × {repetitions} repetitions · estimated ${estimate:0.00} · cap ${cap:0.00}{(sessions > 0 ? $" · {sessions} agent sessions, cap {maxSessions}" : "")}");
        if ((estimate > cap || (sessions > 0 && sessions > maxSessions)) && !options.ContainsKey("force"))
        {
            Console.Error.WriteLine(sessions > maxSessions && sessions > 0
                ? "The run needs more agent sessions than --max-sessions."
                : "The estimate is above the cap: pick fewer questions, systems or models, or add --force (the caps still stop the run).");
            return 2;
        }

        var description = $"Data: {Path.GetFileName(data)} · subset seed {subset.Seed}, {subset.PerAbility} per ability · {string.Join(" · ", systems.Select(s => $"{s.Name} ({s.Model})").Distinct())}";
        var journal = options.GetValueOrDefault("journal");
        var result = await new LmeRun(EvalCli.CreateJudge(), cap, sessions > 0 ? maxSessions : int.MaxValue, Console.Out, description, journal)
            .RunAsync(instances, systems, repetitions, CancellationToken.None);
        var folder = await LmeReport.WriteAsync(result, options.GetValueOrDefault("out", Path.Combine("evals", "results")));
        Console.WriteLine();
        Console.WriteLine(LmeReport.Markdown(result));
        Console.WriteLine($"Written to {Path.GetFullPath(folder)}");
        service?.Dispose();
        return 0;
    }

    /// <summary>The estimate in dollars: Filum's turns from the history's size, the references from their prompt, the judge apart.</summary>
    public static decimal Estimate(IReadOnlyList<LmeInstance> instances, IReadOnlyList<ILmeSystem> systems, string? modelsFile)
    {
        var prices = modelsFile is null ? new Dictionary<string, (decimal In, decimal Out)>() : Prices(modelsFile);
        var total = 0m;
        foreach (var system in systems)
        {
            if (!prices.TryGetValue(system.Model, out var price))
            {
                continue;
            }

            foreach (var instance in instances)
            {
                var (input, output) = system switch
                {
                    HostLmeSystem => LmeEstimate.FilumTokens(instance),
                    NaiveRetrievalSystem => (Math.Min(instance.HistoryCharacters, 5 * instance.HistoryCharacters / Math.Max(1, instance.HaystackSessions.Count)) / 4 + 300, LmeEstimate.OutputTokensPerTurn),
                    _ => (300, LmeEstimate.OutputTokensPerTurn)
                };
                total += LmeEstimate.Usd(input, output, price.In, price.Out);
            }
        }

        return total + instances.Count * systems.Count * LmeEstimate.Usd(LmeEstimate.JudgeInputTokens, 5, EvalCli.JudgeInputPrice, EvalCli.JudgeOutputPrice);
    }

    private static Dictionary<string, (decimal In, decimal Out)> Prices(string modelsFile)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(modelsFile), new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return document.RootElement.GetProperty("Models").EnumerateArray()
            .ToDictionary(m => m.GetProperty("Id").GetString()!, m => (m.GetProperty("InputPricePerMillionUsd").GetDecimal(), m.GetProperty("OutputPricePerMillionUsd").GetDecimal()));
    }
}
