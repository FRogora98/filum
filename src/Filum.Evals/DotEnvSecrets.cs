namespace Filum.Evals;

// From the environment, or the nearest .env file above a folder (also in a source/ folder there).
internal sealed class DotEnvSecrets(string startDirectory)
{
    private const string FileName = ".env";
    private const string SourceFolder = "source";
    private const char Separator = '=';

    public string? Find(string name)
    {
        if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, FileName), Path.Combine(dir.FullName, SourceFolder, FileName) })
            {
                if (File.Exists(candidate) && ValueIn(candidate, name) is { } value)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ValueIn(string file, string name)
    {
        var line = File.ReadLines(file).FirstOrDefault(l => l.TrimStart().StartsWith(name + Separator, StringComparison.Ordinal));
        return line is null ? null : line[(line.IndexOf(Separator) + 1)..].Trim().Trim('"');
    }
}
