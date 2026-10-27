namespace Filum.Evals.Mcp;

// Deleted after the run when nothing holds it any more.
internal sealed class RunFolder
{
    private RunFolder(string path) => FullPath = path;

    public string FullPath { get; }

    public static RunFolder Create(string parent)
    {
        var path = Path.Combine(Path.GetTempPath(), parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new RunFolder(path);
    }

    public void Delete()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            // A server that has not exited yet may still hold a file: the temporary folder is left to the system.
        }
    }
}
