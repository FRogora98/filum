namespace Filum.Evals;

internal static class ResultsFolder
{
    public const string ReportFile = "report.md";
    public const string ResultsFile = "results.json";
    public const string TimeFormat = "yyyyMMdd-HHmmss";

    private const string EvalsFolder = "evals";
    private const string ResultsSubfolder = "results";

    public static string Default { get; } = Path.Combine(EvalsFolder, ResultsSubfolder);

    public static async Task<string> WriteAsync(string folder, IReadOnlyList<(string Name, string Content)> files, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        foreach (var (name, content) in files)
        {
            await File.WriteAllTextAsync(Path.Combine(folder, name), content, cancellationToken);
        }

        return folder;
    }
}
