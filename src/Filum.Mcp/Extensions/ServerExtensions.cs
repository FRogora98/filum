using Filum.Mcp.Server;
using Filum.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.Extensions;

internal static class ServerExtensions
{
    public static IServiceCollection AddFilumMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer().WithStdioServerTransport();
        services.AddSingleton<EngineToolCatalog>();
        services.AddSingleton<IServerToolStrategy, LogToolStrategy>();
        services.AddSingleton<IServerToolStrategy, ConsolidateToolStrategy>();
        services.AddSingleton<IServerToolStrategy, ImportToolStrategy>();
        services.AddTransient<IConfigureOptions<McpServerOptions>, McpServerConfiguration>();
        return services;
    }
}
