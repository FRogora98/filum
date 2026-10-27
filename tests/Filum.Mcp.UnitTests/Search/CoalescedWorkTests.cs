using System.Collections.Concurrent;
using Filum.Mcp.Search;

namespace Filum.Mcp.UnitTests.Search;

public sealed class CoalescedWorkTests
{
    private readonly ConcurrentQueue<Exception> _failures = new();

    [Fact]
    public async Task Request_WhenNothingRuns_ShouldRunTheWorkOnce()
    {
        var runs = 0;
        var work = Work(_ => { Interlocked.Increment(ref runs); return Task.CompletedTask; });

        await work.Request();

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Request_WhenAskedWhileARunIsInProgress_ShouldRunOnceMoreAfterIt()
    {
        var runs = 0;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var work = Work(async _ =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                started.SetResult();
                await release.Task;
            }
        });

        var first = work.Request();
        await started.Task;
        var second = work.Request();
        var third = work.Request();
        release.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.True(second.IsCompleted && third.IsCompleted);
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task Request_WhenManyAskAtOnce_ShouldNeverLoseTheLastRequest()
    {
        for (var round = 0; round < 2000; round++)
        {
            var asked = 0;
            var seen = 0;
            var work = Work(async _ =>
            {
                await Task.Yield();
                InterlockedMax(ref seen, Volatile.Read(ref asked));
            });

            // Task.Run unwraps the task each request returns: this waits for every run the requests started.
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                Interlocked.Increment(ref asked);
                return work.Request();
            })));

            Assert.Equal(asked, seen);
        }
    }

    [Fact]
    public async Task Request_WhenTheWorkFails_ShouldReportItAndRunAgainOnTheNextRequest()
    {
        var runs = 0;
        var work = Work(_ => Interlocked.Increment(ref runs) == 1 ? throw new InvalidOperationException("broken") : Task.CompletedTask);

        await work.Request();
        await work.Request();

        Assert.Equal(2, runs);
        Assert.IsType<InvalidOperationException>(Assert.Single(_failures));
    }

    [Fact]
    public async Task Request_WhenTheServerIsStopping_ShouldNotRun()
    {
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var runs = 0;
        var work = new CoalescedWork(_ => { runs++; return Task.CompletedTask; }, _failures.Enqueue, stopping.Token);

        await work.Request();

        Assert.Equal(0, runs);
        Assert.Empty(_failures);
    }

    private CoalescedWork Work(Func<CancellationToken, Task> work) => new(work, _failures.Enqueue, CancellationToken.None);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
