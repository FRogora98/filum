using Filum.Mcp.Extensions;
using Filum.Mcp.Hook;
using Filum.Mcp.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// `filum-mcp log` and `filum-mcp brief` are a host's hooks, not the server.
if (args is [HookLog.Command, ..])
{
    return await HookLog.RunAsync(args, Console.In, Console.Out, Console.Error, CancellationToken.None);
}

if (args is [HookTidy.Command, ..])
{
    return await HookTidy.RunAsync(args, Console.In, Console.Out, Console.Error, CancellationToken.None);
}

if (args is [HookBrief.Command, ..])
{
    return await HookBrief.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddFilumMcpLogging()
    .AddFilumMcpOptions(builder.Configuration)
    .AddFilumMcpMemory(builder.Configuration)
    .AddFilumMcpServer();

// The memory opens before the server starts: when it cannot, nothing has been written on stdout.
using var host = builder.Build();
if (!await host.Services.GetRequiredService<McpStartup>().OpenAsync(CancellationToken.None))
{
    return 1;
}

await host.RunAsync();
return 0;
