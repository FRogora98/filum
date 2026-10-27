using Filum.Evals.Cli;
using System.Globalization;
using System.Text;

namespace Filum.Evals;

// Run by hand: never part of dotnet test or CI.
internal static class Program
{
    private const string McpCommandName = "mcp";
    private const string LongMemEvalCommandName = "longmemeval";
    private const string PreloadCommandName = "preload";

    public static async Task<int> Main(string[] args)
    {
        // Reports read the same on every console: UTF-8 symbols, numbers with a decimal point.
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var secrets = new DotEnvSecrets(Directory.GetCurrentDirectory());
        var time = TimeProvider.System;
        // Ctrl+C ends the process at once, as it always has: a run stopped halfway writes no report.
        var cancellationToken = CancellationToken.None;
        return args switch
        {
            [McpCommandName, .. var rest] => await new McpCommand(secrets, time).RunAsync(rest, cancellationToken),
            [LongMemEvalCommandName, .. var rest] => await new LongMemEvalCommand(secrets, time).RunAsync(rest, cancellationToken),
            [PreloadCommandName, .. var rest] => await PreloadCommand.RunAsync(rest, cancellationToken),
            _ => await new ScenarioCommand(secrets, time).RunAsync(args, cancellationToken)
        };
    }
}
