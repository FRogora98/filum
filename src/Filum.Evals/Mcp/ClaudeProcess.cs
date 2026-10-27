using System.Diagnostics;

namespace Filum.Evals.Mcp;

// One `claude` process: the message on stdin, its output and errors read to the end.
internal static class ClaudeProcess
{
    public const string Command = "claude";

    public static ProcessStartInfo Create(IEnumerable<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(Command) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        if (OperatingSystem.IsWindows())
        {
            // claude is a script on Windows: run it through the shell, which finds claude.cmd on the PATH.
            start.ArgumentList.Insert(0, Command);
            start.ArgumentList.Insert(0, EvalsConstants.Shell.WindowsRun);
            start.FileName = EvalsConstants.Shell.Windows;
        }

        return start;
    }

    public static async Task<(int ExitCode, string Output, string Errors)> RunAsync(ProcessStartInfo start, string input, CancellationToken cancellationToken)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The agent could not start.");
        // The message goes in on stdin: a history can be far longer than a command line may be.
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await output, await errors);
    }
}
