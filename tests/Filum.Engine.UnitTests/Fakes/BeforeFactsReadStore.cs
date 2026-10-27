using System.Reflection;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Facts;

namespace Filum.Engine.UnitTests.Fakes;

public class BeforeFactsReadStore : DispatchProxy
{
    private IMemoryStore _inner = null!;
    private int _factsReads;

    public Func<Task>? Once { get; set; }

    public static IMemoryStore Wrap(IMemoryStore inner)
    {
        var proxy = Create<IMemoryStore, BeforeFactsReadStore>();
        ((BeforeFactsReadStore)(object)proxy)._inner = inner;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(IMemoryStore.FindLiveAsync) && Equals(args![1], FactTable.Path) && Once is { } once && ++_factsReads == 2)
        {
            Once = null;
            once().GetAwaiter().GetResult();
        }

        return targetMethod.Invoke(_inner, args);
    }
}
