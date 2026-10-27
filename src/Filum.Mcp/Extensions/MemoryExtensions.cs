using Filum.Mcp.Configuration;
using Filum.Mcp.Session;
using Filum.Mcp.Search;
using Filum.Mcp.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Filum.Mcp.Extensions;

internal static class MemoryExtensions
{
    // The search model loads in the background unless FILUM_EMBEDDINGS=off, which downloads nothing; the reranker only
    // with FILUM_RERANKER=on (spec 042).
    public static IServiceCollection AddFilumMcpMemory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OpenedMemory>();
        services.AddSingleton<McpStartup>();
        services.AddSingleton(ServerSession.New());
        services.AddSingleton<ModelSlot>();
        services.AddSingleton<RerankerSlot>();
        services.AddSingleton<McpEngines>();
        services.AddSingleton<VectorMaker>();
        var options = configuration.Get<McpOptions>() ?? new McpOptions();
        if (options.SearchByMeaning)
        {
            services.AddHostedService<ModelLoader>();
        }

        if (options.Reranks)
        {
            services.AddHostedService<RerankerLoader>();
        }

        return services;
    }
}
