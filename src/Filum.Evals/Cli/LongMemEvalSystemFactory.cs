using Filum.Evals.LongMemEval;
using Filum.Evals.LongMemEval.Systems;
using Filum.Evals.Mcp;
using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.Cli;

internal sealed class LongMemEvalSystemFactory(CommandLineArguments arguments, DotEnvSecrets secrets)
{
    // Set to 1, it keeps Claude Code's memory folder after each question, to read what the agent saved.
    public const string KeepVariable = "FILUM_EVALS_KEEP";

    private const string KeepValue = "1";

    private const string DefaultAgentModel = "claude";

    private static readonly TimeSpan HostTimeout = TimeSpan.FromMinutes(10);

    // The caller disposes it.
    public HttpClient? Service { get; private set; }

    public IReadOnlyList<ILongMemEvalSystemStrategy> Create(IReadOnlyList<string> models, ModelsFile? modelsFile)
    {
        var systems = new List<ILongMemEvalSystemStrategy>();
        if (arguments.TryGet(OptionNames.Host, out var label))
        {
            Service = SharedOptions.HostClient(arguments, HostTimeout);
            var target = SharedOptions.Target(arguments);
            systems.AddRange(models.Select(m => new HostSystemStrategy(Service, target, m, label, arguments.Has(OptionNames.Consolidate))));
        }

        foreach (var baseline in CommandLineArguments.Split(arguments.Get(OptionNames.Baselines, string.Empty)))
        {
            var file = modelsFile ?? throw new InvalidOperationException("--baselines needs --models-file.");
            systems.AddRange(models.Select(m => Baseline(baseline, file, m)));
        }

        foreach (var mode in CommandLineArguments.Split(arguments.Get(OptionNames.Claude, string.Empty)))
        {
            systems.Add(Claude(mode));
        }

        return systems;
    }

    // The model is loaded only for a reference that exists.
    private ILongMemEvalSystemStrategy Baseline(string name, ModelsFile file, string model) => name switch
    {
        EvalsConstants.LongMemEvalSystems.None => new NoMemorySystemStrategy(DirectModel.Load(file, model, secrets)),
        EvalsConstants.LongMemEvalSystems.Naive => new NaiveRetrievalSystemStrategy(DirectModel.Load(file, model, secrets)),
        _ => throw new InvalidOperationException($"Unknown baseline '{name}': none or naive.")
    };

    private ClaudeSystemStrategy Claude(string mode)
    {
        var agent = new ClaudeCodeAgentStrategy(arguments.Get(OptionNames.AgentModel));
        var filumMcp = Path.GetFullPath(arguments.Get(OptionNames.FilumMcp) ?? throw new InvalidOperationException("--claude needs --filum-mcp <path>."));
        var claudeMode = mode switch
        {
            EvalsConstants.LongMemEvalSystems.Filum => ClaudeSystemMode.WithFilum,
            EvalsConstants.LongMemEvalSystems.None => ClaudeSystemMode.NoMemory,
            EvalsConstants.LongMemEvalSystems.Full => ClaudeSystemMode.FullContext,
            _ => throw new InvalidOperationException($"Unknown Claude mode '{mode}': filum, none or full.")
        };
        return new ClaudeSystemStrategy(agent, claudeMode, filumMcp, agent.Model ?? DefaultAgentModel, arguments.Has(OptionNames.Consolidate), arguments.Has(OptionNames.Hook),
            keepMemory: Environment.GetEnvironmentVariable(KeepVariable) == KeepValue);
    }
}
