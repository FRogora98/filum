using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;

namespace Filum.Engine.Testing;

/// <summary>
/// What <see cref="IMemoryService"/> guarantees on any <see cref="IMemoryStore"/>: the engine's tests run it in memory
/// and on a local folder, and every other store runs it in its own tests. Every test uses fresh user ids.
/// </summary>
public abstract partial class MemoryServiceContract
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    // Small, so they are easy to reach.
    private static readonly MemoryOptions SmallLimits = new() { MaxFileBytes = 2048, MaxCollectionBytes = 8192, MaxFilesPerUser = 8, MaxCoreChars = 300 };

    private readonly Guid _user = Guid.NewGuid();
    private IMemoryService? _small;

    /// <summary>
    /// A memory with these limits over the store under test; every call of one test sees the same store.
    /// <paramref name="beforeSave"/> runs right before each save.
    /// </summary>
    protected abstract IMemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null);

    private IMemoryService _memory => _small ??= Memory(SmallLimits);

    /// <summary>The person every test writes for; a one-person store is opened for them.</summary>
    protected Guid Person => _user;

    /// <summary>
    /// A store that holds one person only (a local folder): another person sees nothing and cannot write, instead of
    /// having a memory of their own next to the first one's.
    /// </summary>
    protected virtual bool OnePersonPerStore => false;

    /// <summary>The store under test, for the calls the service does not expose.</summary>
    protected abstract IMemoryStore StoreOf(IMemoryService memory);

    // Its changes all wait for each other right before saving, so they race from the same version.
    private IMemoryService RacingService(int racers)
    {
        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return Memory(SmallLimits,
            async _ =>
            {
                if (Interlocked.Increment(ref arrived) == racers)
                {
                    gate.SetResult();
                }

                await gate.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            });
    }

    private static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.True(!outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }

    private static string Refused<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.True(outcome.IsRefused, "expected a refusal");
        return outcome.Refusal!;
    }
}
