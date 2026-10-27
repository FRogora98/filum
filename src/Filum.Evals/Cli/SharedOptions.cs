using Filum.Evals.Hosts;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Cli;

/// <summary>The options several commands read the same way: the host and the scenarios.</summary>
internal static class SharedOptions
{
    private const string ScenariosFolder = "scenarios";

    public static HostTarget Target(CommandLineArguments arguments)
    {
        var register = arguments.Get(OptionNames.Register);
        var header = arguments.Get(OptionNames.PersonHeader);
        if (register is null && header is null)
        {
            throw new InvalidOperationException("A host needs --register <its registration path> or --person-header <name>.");
        }

        return new(arguments.Get(OptionNames.Prefix, HostTarget.DefaultPrefix), register, header, arguments.Get(OptionNames.Setup));
    }

    public static HttpClient HostClient(CommandLineArguments arguments, TimeSpan timeout) =>
        new()
        {
            BaseAddress = new Uri(arguments.Get(OptionNames.Service) ?? throw new InvalidOperationException("A host needs --service <its url>.")),
            Timeout = timeout
        };

    /// <summary>The scenarios of the folder given (the synthetic ones next to the runner by default), filtered by id.</summary>
    public static IReadOnlyList<Scenario> Scenarios(CommandLineArguments arguments) =>
        ScenarioLoader.LoadAll(arguments.Get(OptionNames.ScenariosDir) ?? Path.Combine(AppContext.BaseDirectory, ScenariosFolder), arguments.Get(OptionNames.Scenarios));
}
