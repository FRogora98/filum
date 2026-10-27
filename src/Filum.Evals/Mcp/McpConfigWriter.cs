using System.Text.Json;

namespace Filum.Evals.Mcp;

internal sealed class McpConfigWriter(string filumMcp, bool hook = false)
{
    // The agent puts it in front of its tools.
    public const string ServerName = "filum";

    public const string SettingsFile = "claude-settings.json";

    public const string ConfigFile = "mcp.json";

    private const string HomeVariable = "FILUM_HOME";

    // The scenarios use every tool (collections, skills, history), not only the core profile's.
    private const string ToolsVariable = "FILUM_TOOLS";
    private const string FullProfile = "full";
    private const string DotnetHost = "dotnet";

    private bool IsDll => filumMcp.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    public async Task<string> WriteConfigAsync(string root, bool withFilum, string home, CancellationToken cancellationToken)
    {
        var config = Path.Combine(root, ConfigFile);
        await File.WriteAllTextAsync(config, McpConfig(withFilum, home), cancellationToken);
        if (withFilum && hook)
        {
            await File.WriteAllTextAsync(Path.Combine(root, SettingsFile), HookSettings(home), cancellationToken);
        }

        return config;
    }

    public string McpConfig(bool withFilum, string home)
    {
        var servers = new Dictionary<string, object>();
        if (withFilum)
        {
            servers[ServerName] = new
            {
                command = IsDll ? DotnetHost : filumMcp,
                args = IsDll ? new[] { filumMcp } : [],
                env = new Dictionary<string, string> { [HomeVariable] = home, [ToolsVariable] = FullProfile }
            };
        }

        return JsonSerializer.Serialize(new { mcpServers = servers });
    }

    public string HookSettings(string home)
    {
        static string Quoted(string path) => $"\"{path.Replace('\\', '/')}\"";
        var command = $"{(IsDll ? DotnetHost + " " + Quoted(filumMcp) : Quoted(filumMcp))} log --home {Quoted(home)}";
        return JsonSerializer.Serialize(new
        {
            hooks = new Dictionary<string, object>
            {
                ["UserPromptSubmit"] = new[] { new { hooks = new[] { new { type = "command", command } } } }
            }
        });
    }
}
