using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Testing;

namespace Filum.Engine.Storage.InMemory.UnitTests;

public sealed class InMemoryMemoryStoreTests : MemoryServiceContract
{
    private readonly InMemoryMemoryStore _store = new();

    protected override IMemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null) =>
        MemoryServices.On(_store, limits, beforeSave);

    protected override IMemoryStore StoreOf(IMemoryService memory) => _store;
}
