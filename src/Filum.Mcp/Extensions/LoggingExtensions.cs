using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Filum.Mcp.Extensions;

internal static class LoggingExtensions
{
    private const string OwnCategories = "Filum.Mcp";

    // On stderr, since stdout carries the protocol only: the engine's and the SDK's from warnings up, the server's own
    // from information up.
    public static IServiceCollection AddFilumMcpLogging(this IServiceCollection services) =>
        services.AddLogging(logging => logging
            .ClearProviders()
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace)
            .SetMinimumLevel(LogLevel.Warning)
            .AddFilter(OwnCategories, LogLevel.Information));
}
