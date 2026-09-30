using Filum.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

// stdout carries the protocol only: every log line goes to stderr.
LocalFolderStore store;
var folder = LocalFolderStore.DefaultFolder();
try
{
    store = new LocalFolderStore(folder);
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"filum-mcp: cannot use the memory folder {Path.GetFullPath(folder)}: {e.Message} Set FILUM_HOME to a folder you can write.");
    return 1;
}

// A package (spec 016) is optional; a package that is set but cannot be used stops the server.
var limits = new MemoryOptions();
Pack? pack = null;
if (Environment.GetEnvironmentVariable("FILUM_PACK") is { Length: > 0 } packFolder)
{
    try
    {
        pack = Pack.Load(packFolder, limits);
        Console.Error.WriteLine($"filum-mcp: package {pack.Name} {pack.Version} from {Path.GetFullPath(packFolder)}");
    }
    catch (PackException e)
    {
        Console.Error.WriteLine($"filum-mcp: {e.Message}");
        return 1;
    }
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

using var logging = LoggerFactory.Create(l => l.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
builder.Services
    .AddMcpServer(o =>
    {
        o.ServerInfo = new Implementation { Name = "filum", Version = typeof(McpInstructions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" };
        o.ServerInstructions = McpInstructions.Text + (pack?.Prompt is null ? string.Empty : "\n" + PlatformInstructions.PackSection(pack));
    })
    .WithStdioServerTransport()
    .WithTools(McpServerSetup.Tools(store, limits, pack, logging));

await builder.Build().RunAsync();
return 0;
