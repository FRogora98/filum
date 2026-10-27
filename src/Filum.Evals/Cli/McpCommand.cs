using Filum.Evals.Mcp;
using Filum.Evals.Runs;
using System.Globalization;

namespace Filum.Evals.Cli;

// The agent's own file and memory tools are off in both arms, so only Filum can remember. Run by hand: every session
// uses the owner's own agent.
internal sealed class McpCommand(DotEnvSecrets secrets, TimeProvider time)
{
    public const string Help = """
        The growth test of filum-mcp: each chat a session of a real agent, continued turn by turn, with filum-mcp and without.

          dotnet run --project src/Filum.Evals -- mcp --filum-mcp <path> --max-sessions 20 [options]

          --filum-mcp <path>    the filum-mcp to mount (required): a release's executable, or a build's filum-mcp.dll
          --max-sessions <n>    the most agent sessions of the run (required); a turn is one session per arm
          --force               start even if the scenarios need more sessions (the cap still stops the run)
          --agent-model <name>  the agent's model, for example haiku (default: the agent's own)
          --hook                every message of the person kept by filum-mcp log, as a Claude Code hook (spec 036)
          --filum-only          only the arm with filum-mcp, when the one without is already known
          --scenarios-dir <dir> --scenarios <glob> --reps <n> (default 1) --cap <usd> (the judge, default 0.5) --out <dir>
        """;

    private const int DefaultRepetitions = 1;
    private const decimal DefaultCapUsd = 0.5m;

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var arguments = CommandLineArguments.Parse(args);
        if (arguments.Has(OptionNames.Help) || !arguments.TryGet(OptionNames.MaxSessions, out var max) || !arguments.TryGet(OptionNames.FilumMcp, out var filumMcp))
        {
            Console.WriteLine(Help);
            return arguments.Has(OptionNames.Help) ? 0 : 1;
        }

        var scenarios = SharedOptions.Scenarios(arguments);
        var repetitions = arguments.Int(OptionNames.Reps, DefaultRepetitions);
        var sessions = SessionBudget.Needed(scenarios, repetitions);
        var budget = new SessionBudget(int.Parse(max, CultureInfo.InvariantCulture));
        // --filum-only plays the arm with filum alone, when the arm without it is already known (spec 056).
        string[] arms = arguments.Has(OptionNames.FilumOnly) ? [McpArms.WithFilum] : [McpArms.WithFilum, McpArms.WithoutFilum];
        Console.WriteLine($"{scenarios.Count} scenarios × {arms.Length} arms × {repetitions} repetitions · {sessions} agent sessions · cap {budget.Max} sessions");
        if (sessions > budget.Max && !arguments.Has(OptionNames.Force))
        {
            Console.Error.WriteLine("The run needs more sessions than --max-sessions: pick fewer scenarios or repetitions, or add --force (the cap still stops the run).");
            return EvalsConstants.ExitCodes.OverCap;
        }

        var agent = new ClaudeCodeAgentStrategy(arguments.Get(OptionNames.AgentModel));
        var runner = new McpScenarioRunnerStrategy(agent, CommandJudge.Create(secrets), Path.GetFullPath(filumMcp), budget, hook: arguments.Has(OptionNames.Hook));
        var cap = arguments.Decimal(OptionNames.Cap, DefaultCapUsd);
        var result = await new EvalRun(runner, cap, Console.Out, $"filum-mcp in {agent.Name}{(agent.Model is null ? "" : $" ({agent.Model})")}")
            .RunAsync(scenarios, arms, repetitions, cancellationToken);
        var folder = await EvalReport.WriteAsync(result, arguments.OutDirectory(), time, cancellationToken);
        CommandOutput.Report(EvalReport.Markdown(result, time), folder);
        return EvalsConstants.ExitCodes.Success;
    }
}
