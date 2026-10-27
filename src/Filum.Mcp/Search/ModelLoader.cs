using Filum.Embeddings;
using Filum.Mcp.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.Search;

// Whatever goes wrong, the server goes on searching by words.
internal sealed partial class ModelLoader(IOptions<McpOptions> options, ModelSlot slot, ILogger<ModelLoader> logger) : BackgroundService
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = options.Value.ModelFolder;
        try
        {
            if (!ModelFiles.Present(folder))
            {
                LogDownloading(ModelFiles.Name, folder);
                using var http = new HttpClient { Timeout = DownloadTimeout };
                if (!await ModelFiles.EnsureAsync(folder, http, stoppingToken))
                {
                    LogHashMismatch();
                    return;
                }
            }

            Fill(E5Embedder.TryLoad(folder));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The model only adds search by meaning: without it the server still works, by words.
            LogNotLoaded(e.GetType().Name);
        }
    }

    private void Fill(E5Embedder? embedder)
    {
        if (embedder is null)
        {
            LogIncomplete();
            return;
        }

        if (!slot.TryFill(embedder))
        {
            embedder.Dispose();
            return;
        }

        LogLoaded();
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = McpConstants.ProgramName + ": downloading the search model ({Model}, about 120 MB) into {Folder}")]
    private partial void LogDownloading(string model, string folder);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the search model did not match its hash; searching by words only")]
    private partial void LogHashMismatch();

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the search model is incomplete; searching by words only")]
    private partial void LogIncomplete();

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = McpConstants.ProgramName + ": the search model is loaded")]
    private partial void LogLoaded();

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the search model could not load ({Error}); searching by words only")]
    private partial void LogNotLoaded(string error);
}
