using Filum.Engine.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tests;

/// <summary>The memory's contract on the in-memory store: the engine, tested with no database.</summary>
public sealed class InMemoryMemoryServiceTests : MemoryServiceContract
{
    private readonly InMemoryMemoryStore _store = new();

    protected override MemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null) =>
        new(_store, Options.Create(limits), NullLogger<MemoryService>.Instance, beforeSave);
}
