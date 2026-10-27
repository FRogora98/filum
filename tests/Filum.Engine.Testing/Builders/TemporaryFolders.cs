namespace Filum.Engine.Testing.Builders;

// A test's folder is deleted when the test ends. On Windows another process (an antivirus, the search indexer) may hold a
// file just written for a moment, and the deletion fails although the test passed: it is tried again for a while.
public static class TemporaryFolders
{
    private const int Tries = 20;
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(100);

    public static void Delete(string folder)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }

                return;
            }
            catch (Exception e) when ((e is IOException or UnauthorizedAccessException) && attempt < Tries)
            {
                Thread.Sleep(Wait);
            }
        }
    }
}
