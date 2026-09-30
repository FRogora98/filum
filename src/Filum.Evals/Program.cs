using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ClientModel;
using System.Globalization;
using System.Net.Http.Json;

namespace Filum.Evals;

/// <summary>Model evals, run by hand against a running host of the engine. Never part of dotnet test or CI.</summary>
public static class EvalCli
{
    public const string JudgeModel = "gpt-5.4-mini";
    public const decimal JudgeInputPrice = 0.75m;
    public const decimal JudgeOutputPrice = 4.50m;

    public static async Task<int> Main(string[] args)
    {
        if (args is ["mcp", .. var mcpArgs])
        {
            return await McpEvalCli.MainAsync(mcpArgs);
        }

        const string help = """
            Filum evals: plays scenarios against a running service that hosts the engine and compares models.

              dotnet run --project src/Filum.Evals -- --models <ids|host> [options]
              dotnet run --project src/Filum.Evals -- mcp [options]     (filum-mcp in a real agent, with and without it)

              --models <ids>         catalog ids, comma-separated (see the host's models group); "host" for the host's own model
              --service <url>        the host (default: http://localhost:5410)
              --prefix <path>        where the host mapped the groups (default: /api)
              --register <path>      register a new synthetic account there, per run (default: /api/auth/register)
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

        var options = Args.Parse(args);
        if (options.ContainsKey("help") || !options.ContainsKey("models"))
        {
            Console.WriteLine(help);
            return options.ContainsKey("help") ? 0 : 1;
        }

        var models = options["models"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var repetitions = int.Parse(options.GetValueOrDefault("reps", "3"), CultureInfo.InvariantCulture);
        var cap = decimal.Parse(options.GetValueOrDefault("cap", "2"), CultureInfo.InvariantCulture);
        var serviceUrl = options.GetValueOrDefault("service", "http://localhost:5410");
        var outDir = options.GetValueOrDefault("out", Path.Combine("evals", "results"));
        var target = new HostTarget(
            options.GetValueOrDefault("prefix", "/api"),
            options.GetValueOrDefault("register", "/api/auth/register"),
            options.GetValueOrDefault("person-header"),
            options.GetValueOrDefault("setup"));

        var scenarios = ScenarioLoader.LoadAll(options.GetValueOrDefault("scenarios-dir") ?? Path.Combine(AppContext.BaseDirectory, "scenarios"), options.GetValueOrDefault("scenarios"));
        using var service = new HttpClient { BaseAddress = new Uri(serviceUrl), Timeout = TimeSpan.FromMinutes(5) };

        decimal? estimate = null;
        if (!models.SequenceEqual([HostTarget.HostModel]))
        {
            var catalog = await service.GetFromJsonAsync<List<ModelDto>>($"{target.Prefix.TrimEnd('/')}/models") ?? [];
            var unknown = models.Where(m => catalog.All(c => c.Id != m)).ToList();
            if (unknown.Count > 0)
            {
                Console.Error.WriteLine($"Not in the host's catalog (or its provider has no key): {string.Join(", ", unknown)}. Available: {string.Join(", ", catalog.Select(c => c.Id))}");
                return 1;
            }

            estimate = CostEstimate.Of(scenarios, catalog.Where(c => models.Contains(c.Id)).ToList(), repetitions, JudgeInputPrice, JudgeOutputPrice);
        }

        Console.WriteLine($"{scenarios.Count} scenarios × {models.Length} models × {repetitions} repetitions · {(estimate is { } e ? $"estimated ${e:0.00}" : "no estimate for the host's own model")} · cap ${cap:0.00}");
        if (estimate > cap && !options.ContainsKey("force"))
        {
            Console.Error.WriteLine("The estimate is above the cap: lower --reps, pick fewer models or scenarios, or add --force (the cap still stops the run).");
            return 2;
        }

        var judge = CreateJudge();
        var result = await new EvalRun(new ScenarioRunner(service, judge, target), cap, Console.Out, target.Describe(service.BaseAddress))
            .RunAsync(scenarios, models, repetitions, CancellationToken.None);

        var folder = await EvalReport.WriteAsync(result, outDir);
        Console.WriteLine();
        Console.WriteLine(EvalReport.Markdown(result));
        Console.WriteLine($"Written to {Path.GetFullPath(folder)}");
        return 0;
    }

    public static ModelClaimJudge CreateJudge()
    {
        var openAIKey = DotEnv.Find("OPENAI_API_KEY") ?? throw new InvalidOperationException("OPENAI_API_KEY is needed for the judge: set it in the environment or in a .env file.");
        return new ModelClaimJudge(new ChatClient(JudgeModel, new ApiKeyCredential(openAIKey)).AsIChatClient(), JudgeInputPrice, JudgeOutputPrice);
    }
}

internal static class Args
{
    /// <summary>"--name value" pairs and "--flag" switches.</summary>
    public static Dictionary<string, string> Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = args[i][2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options[name] = hasValue ? args[++i] : "true";
        }

        return options;
    }
}

internal static class DotEnv
{
    /// <summary>A value from the environment, or from the nearest .env file above the current directory (also in a source/ folder there).</summary>
    public static string? Find(string name)
    {
        if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, ".env"), Path.Combine(dir.FullName, "source", ".env") })
            {
                if (File.Exists(candidate))
                {
                    var line = File.ReadLines(candidate).FirstOrDefault(l => l.TrimStart().StartsWith(name + "=", StringComparison.Ordinal));
                    if (line is not null)
                    {
                        return line[(line.IndexOf('=') + 1)..].Trim().Trim('"');
                    }
                }
            }
        }

        return null;
    }
}
