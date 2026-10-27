using Filum.Engine.Abstractions.Options;
using Filum.Engine.Packs;
using Filum.Engine.Storage.LocalFolder;
using Filum.Mcp.Configuration;
using Filum.Mcp.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.Startup;

// Anything that cannot be used stops the server with a line on stderr, before anything is written on stdout.
internal sealed partial class McpStartup(IOptions<McpOptions> options, IOptions<MemoryOptions> limits, OpenedMemory memory, McpEngines engines, ILogger<McpStartup> logger)
{
    public async Task<bool> OpenAsync(CancellationToken cancellationToken)
    {
        McpOptions settings;
        try
        {
            settings = options.Value;
        }
        catch (OptionsValidationException e)
        {
            LogInvalidSettings(e.Message);
            return false;
        }

        if (OpenFolder(settings.Home) is not { } store)
        {
            return false;
        }

        Pack? pack = null;
        if (settings.HasPack)
        {
            pack = await LoadPackAsync(settings.Pack, cancellationToken);
            if (pack is null)
            {
                store.Dispose();
                return false;
            }
        }

        memory.Open(store, store.Owner, pack);
        // A fresh chat sees the rules even when the model calls no tool (spec 056).
        memory.KeepRules(await engines.Current.Memory.RulesAsync(store.Owner, McpConstants.Limits.MaxRulesChars, cancellationToken));
        return true;
    }

    private LocalFolderStore? OpenFolder(string folder)
    {
        try
        {
            return new LocalFolderStore(folder, null, McpClock.Time);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LogFolderUnusable(Path.GetFullPath(folder), e.Message, McpConstants.Variables.Home);
            return null;
        }
    }

    private async Task<Pack?> LoadPackAsync(string folder, CancellationToken cancellationToken)
    {
        var loaded = await PackLoader.TryLoadAsync(folder, limits.Value, cancellationToken);
        if (!loaded.IsLoaded)
        {
            LogPackUnusable(loaded.Message);
            return null;
        }

        var from = Path.GetFullPath(folder);
        LogPackLoaded(loaded.Pack.Name, loaded.Pack.Version, from);
        return loaded.Pack;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = McpConstants.ProgramName + ": {Problems}")]
    private partial void LogInvalidSettings(string problems);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = McpConstants.ProgramName + ": cannot use the memory folder {Folder}: {Reason} Set {Variable} to a folder you can write.")]
    private partial void LogFolderUnusable(string folder, string reason, string variable);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = McpConstants.ProgramName + ": package {Name} {Version} from {Folder}")]
    private partial void LogPackLoaded(string name, string version, string folder);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = McpConstants.ProgramName + ": {Problems}")]
    private partial void LogPackUnusable(string problems);
}
