using Microsoft.EntityFrameworkCore;

namespace Filum.SampleHost;

/// <summary>The sample's own database: the engine's tables next to whatever else a host keeps (here, nothing else).</summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => FilumModel.Configure(modelBuilder);
}
