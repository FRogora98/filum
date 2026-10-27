using System.Reflection;
using System.Runtime.ExceptionServices;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Testing.Fakes;

/// <summary>
/// A store that runs a hook right before every save and then saves in the store it wraps: two changes that wait for
/// each other in the hook race from the same version. Every other call goes straight to the wrapped store.
/// </summary>
public class BeforeSaveStore : DispatchProxy
{
    private IMemoryStore _inner = null!;
    private Func<CancellationToken, Task> _beforeSave = null!;

    /// <summary><paramref name="inner"/>, with <paramref name="beforeSave"/> run right before each save.</summary>
    public static IMemoryStore Wrap(IMemoryStore inner, Func<CancellationToken, Task> beforeSave)
    {
        var proxy = Create<IMemoryStore, BeforeSaveStore>();
        var store = (BeforeSaveStore)(object)proxy;
        store._inner = inner;
        store._beforeSave = beforeSave;
        return proxy;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IMemoryFileStore.SaveAsync))
        {
            return SaveAsync(args!);
        }

        return Forward(targetMethod, args);
    }

    private async Task<SavedChange?> SaveAsync(object?[] args)
    {
        await _beforeSave((CancellationToken)args[^1]!);
        return await _inner.SaveAsync((Guid)args[0]!, (StoredFile?)args[1], (FileState)args[2]!, (MemoryActor)args[3]!, (string)args[4]!, (long?)args[5], (DateTimeOffset)args[6]!, (CancellationToken)args[7]!);
    }

    private object? Forward(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(e.InnerException);
            throw;
        }
    }
}
