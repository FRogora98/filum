namespace Filum.Evals.Cli;

internal static class CommandOutput
{
    public static void Report(string markdown, string folder)
    {
        Console.WriteLine();
        Console.WriteLine(markdown);
        Written(folder);
    }

    public static void Written(string folder) => Console.WriteLine($"Written to {Path.GetFullPath(folder)}");
}
