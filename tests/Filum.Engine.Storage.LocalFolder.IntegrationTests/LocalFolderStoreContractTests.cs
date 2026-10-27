using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Memory;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Testing;

namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

public sealed class LocalFolderStoreContractTests : MemoryServiceContract, IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
    private LocalFolderStore? _store;

    protected override bool OnePersonPerStore => true;

    protected override IMemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null) =>
        MemoryServices.On(_store ??= new LocalFolderStore(_folder, Person), limits, beforeSave);

    protected override IMemoryStore StoreOf(IMemoryService memory) => _store!;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }
}
