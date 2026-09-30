using Filum.Engine.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tests;

/// <summary>The memory's contract on a local folder (spec 012), one person per folder.</summary>
public sealed class LocalMemoryServiceTests : MemoryServiceContract, IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));
    private LocalFolderStore? _store;

    protected override bool OnePersonPerStore => true;

    protected override MemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null) =>
        new(_store ??= new LocalFolderStore(_folder, Person), Options.Create(limits), NullLogger<MemoryService>.Instance, beforeSave);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
