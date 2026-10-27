namespace Filum.Engine.IntegrationTests.Snapshots;

// FILUM_UPDATE_SNAPSHOT=1 rewrites the file for review.
internal static class SnapshotFile
{
    public static void Match(string name, string text)
    {
        var snapshot = Path.Combine(SourceFolder(), "Snapshots", name);
        if (Environment.GetEnvironmentVariable("FILUM_UPDATE_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.WriteAllText(snapshot, text);
        }

        Assert.True(File.Exists(snapshot), $"No snapshot {name} yet: run once with FILUM_UPDATE_SNAPSHOT=1 and review it.");
        Assert.Equal(File.ReadAllText(snapshot).Replace("\r\n", "\n"), text);
    }

    private static string SourceFolder()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.Engine.IntegrationTests.csproj")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("The test project folder was not found.");
        }

        return folder;
    }
}
