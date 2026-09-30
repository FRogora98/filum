using Microsoft.EntityFrameworkCore;

namespace Filum.Agent;

/// <summary>
/// The host's database, as the libraries see it: a context whose model includes <see cref="FilumModel"/>. The host
/// owns the context type, its connection and its migrations.
/// </summary>
public interface IFilumDb
{
    /// <summary>A new short-lived context; the caller disposes it.</summary>
    DbContext CreateContext();
}
