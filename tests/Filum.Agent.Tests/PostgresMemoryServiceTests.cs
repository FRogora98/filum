using Filum.Agent.Tests.Infrastructure;
using Filum.Engine.Testing;
using Filum.SampleHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Agent.Tests;

/// <summary>The memory's contract on Postgres, the hosted product's store, with the engine's tables as a host has them.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PostgresMemoryServiceTests(PostgresFixture postgres) : MemoryServiceContract
{
    private static readonly SemaphoreSlim Created = new(1, 1);
    private static bool _created;

    protected override MemoryService Memory(MemoryOptions limits, Func<CancellationToken, Task>? beforeSave = null)
    {
        var db = new Db(postgres.ConnectionString);
        EnsureCreated(db);
        return new(new PostgresMemoryStore(db), Options.Create(limits), NullLogger<MemoryService>.Instance, beforeSave);
    }

    private static void EnsureCreated(Db db)
    {
        Created.Wait();
        try
        {
            if (!_created)
            {
                using var context = db.CreateContext();
                context.Database.EnsureCreated();
                _created = true;
            }
        }
        finally
        {
            Created.Release();
        }
    }

    private sealed class Db(string connectionString) : IFilumDb
    {
        public DbContext CreateContext() => new SampleDbContext(new DbContextOptionsBuilder<SampleDbContext>().UseNpgsql(connectionString).Options);
    }
}
