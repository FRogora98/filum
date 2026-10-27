using Filum.Embeddings;
using Filum.Mcp.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.Search;

// Runs only with FILUM_RERANKER=on. Whatever goes wrong, the server goes on searching in its own order.
internal sealed partial class RerankerLoader(IOptions<McpOptions> options, RerankerSlot slot, ILogger<RerankerLoader> logger) : BackgroundService
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var folder = options.Value.RerankerFolder;
        try
        {
            if (!RerankerFiles.Present(folder))
            {
                LogDownloading(RerankerFiles.Name, folder);
                using var http = new HttpClient { Timeout = DownloadTimeout };
                if (!await RerankerFiles.EnsureAsync(folder, http, stoppingToken))
                {
                    LogHashMismatch();
                    return;
                }
            }

            if (BgeReranker.TryLoad(folder) is not { } reranker)
            {
                LogIncomplete();
                return;
            }

            if (!slot.TryFill(reranker))
            {
                reranker.Dispose();
                return;
            }

            LogLoaded();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The reranker only sharpens the order: without it the server still searches.
            LogNotLoaded(e.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 30, Level = LogLevel.Information, Message = McpConstants.ProgramName + ": downloading the reranker ({Model}, about 575 MB) into {Folder}")]
    private partial void LogDownloading(string model, string folder);

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the reranker did not match its hash; searching without it")]
    private partial void LogHashMismatch();

    [LoggerMessage(EventId = 32, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the reranker is incomplete; searching without it")]
    private partial void LogIncomplete();

    [LoggerMessage(EventId = 33, Level = LogLevel.Information, Message = McpConstants.ProgramName + ": the reranker is loaded")]
    private partial void LogLoaded();

    [LoggerMessage(EventId = 34, Level = LogLevel.Warning, Message = McpConstants.ProgramName + ": the reranker could not load ({Error}); searching without it")]
    private partial void LogNotLoaded(string error);
}
