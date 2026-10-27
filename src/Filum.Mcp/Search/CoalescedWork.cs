namespace Filum.Mcp.Search;

// A request that comes while it runs makes one more run after it, so no request is lost and requests that pile up cost
// one run.
internal sealed class CoalescedWork(Func<CancellationToken, Task> work, Action<Exception> failed, CancellationToken stopping)
{
    private const int No = 0;
    private const int Yes = 1;

    private int _requested;
    private int _running;

    // The task of the run this request started, or a completed task when one already runs.
    public Task Request()
    {
        Interlocked.Exchange(ref _requested, Yes);
        return Interlocked.CompareExchange(ref _running, Yes, No) == No ? Task.Run(RunAsync, CancellationToken.None) : Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        do
        {
            while (!stopping.IsCancellationRequested && Interlocked.Exchange(ref _requested, No) == Yes)
            {
                await RunOnceAsync();
            }

            Volatile.Write(ref _running, No);
        }
        // A request between the last check and the release above found the work still running: take it here.
        while (!stopping.IsCancellationRequested && Volatile.Read(ref _requested) == Yes && Interlocked.CompareExchange(ref _running, Yes, No) == No);
    }

    private async Task RunOnceAsync()
    {
        try
        {
            await work(stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The server is stopping: the work is left for the next start.
        }
        catch (Exception e)
        {
            failed(e);
        }
    }
}
