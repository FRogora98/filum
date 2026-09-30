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

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

using var logging = LoggerFactory.Create(l => l.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
builder.Services
    .AddMcpServer(o =>
    {
        o.ServerInfo = new Implementation { Name = "filum", Version = typeof(McpInstructions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" };
        o.ServerInstructions = McpInstructions.Text;
    })
    .WithStdioServerTransport()
    .WithTools(McpServerSetup.Tools(store, new MemoryOptions(), logging));

await builder.Build().RunAsync();
return 0;
