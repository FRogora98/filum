namespace Filum.Engine.Storage.LocalFolder;

// One call at a time: first among this process's calls (a semaphore), then among the processes that share the folder (a
// lock file held open without sharing).
internal sealed class FolderLock(string lockFile, TimeProvider time) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(10);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose() => _gate.Dispose();

    public async Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var folderLock = await AcquireAsync(cancellationToken);
            return action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<FileStream> AcquireAsync(CancellationToken cancellationToken)
    {
        var until = time.GetUtcNow() + Timeout;
        while (true)
        {
            try
            {
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (time.GetUtcNow() < until)
            {
                await Task.Delay(Retry, cancellationToken);
            }
        }
    }
}
