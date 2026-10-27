using Filum.Engine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Filum.Engine.Storage.LocalFolder;

/// <summary>Stores an engine's memory in a local folder.</summary>
public static class LocalFolderEngineBuilderExtensions
{
    /// <summary>
    /// Stores the memory in <paramref name="folder"/>, which belongs to one person (<paramref name="owner"/> for a new
    /// folder; a new id when null). The container owns the <see cref="LocalFolderStore"/> and disposes it; a host reads
    /// the folder's owner from it.
    /// </summary>
    public static FilumEngineBuilder UseLocalFolder(this FilumEngineBuilder engine, string folder, Guid? owner = null)
    {
        engine.Services.TryAddSingleton(sp => new LocalFolderStore(folder, owner, sp.GetService<TimeProvider>()));
        return engine.UseStore(sp => sp.GetRequiredService<LocalFolderStore>());
    }
}
