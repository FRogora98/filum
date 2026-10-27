using Filum.Engine.Abstractions.Options;
using Filum.Mcp.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Filum.Mcp.Extensions;

internal static class OptionsExtensions
{
    // Bound at the configuration's root, with no section: every MCP host sets them as plain variables
    // (docs/EXCEPTIONS.md, CFG-01).
    public static IServiceCollection AddFilumMcpOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<McpOptions>()
            .Bind(configuration)
            .PostConfigure(options => options.ApplyDefaults());
        services.AddSingleton<IValidateOptions<McpOptions>, McpOptionsValidator>();
        services.AddOptions<MemoryOptions>();
        return services;
    }
}
