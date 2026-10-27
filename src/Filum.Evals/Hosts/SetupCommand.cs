using System.Diagnostics;

namespace Filum.Evals.Hosts;

// Its failure fails the run.
internal static class SetupCommand
{
    public static async Task<string?> RunAsync(string command, EvalPerson person, CancellationToken cancellationToken)
    {
        var line = command.Replace(EvalsConstants.SetupPlaceholders.Email, person.Email, StringComparison.Ordinal).Replace(EvalsConstants.SetupPlaceholders.Person, person.Id.ToString(), StringComparison.Ordinal);
        // cmd reads its command line raw: quoting it as an argument would turn the command's own quotes into \".
        var start = OperatingSystem.IsWindows() ? new ProcessStartInfo(EvalsConstants.Shell.Windows) { Arguments = $"{EvalsConstants.Shell.WindowsRun} {line}" } : new ProcessStartInfo(EvalsConstants.Shell.Unix, [EvalsConstants.Shell.UnixRun, line]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The setup command could not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? null : $"setup exited with {process.ExitCode}: {(await output + await error).Trim()}";
    }
}
