using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Filum.Mcp;

/// <summary>
/// The engine's catalog as MCP tools. <see cref="MemoryTools"/> is made for one turn (it counts calls and keeps the
/// turn's steps), and here every call is a turn: each call builds fresh tools and runs the one of the same name.
/// </summary>
public static class McpServerSetup
{
    public static IReadOnlyList<McpServerTool> Tools(LocalFolderStore store, MemoryOptions limits, Pack? pack, ILoggerFactory logging)
    {
        var memory = new MemoryService(store, Options.Create(limits), logging.CreateLogger<MemoryService>(), pack: pack);
        var session = Guid.NewGuid();
        return MemoryTools.Catalog(limits)
            .Select(template => McpServerTool.Create(new PerCall(template, () =>
                new MemoryTools(memory, limits, store.Owner, MemoryActor.Agent(session, Guid.NewGuid())))))
            .ToList();
    }

    /// <summary>Runs the function of the template's name on fresh tools; a refusal becomes a tool error.</summary>
    private sealed class PerCall(AIFunction template, Func<MemoryTools> fresh) : DelegatingAIFunction(template)
    {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var function = fresh().Tools.OfType<AIFunction>().Single(t => t.Name == Name);
            // The engine's tools answer in text; the factory hands it back as a JSON string, sent here as plain text.
            var result = await function.InvokeAsync(arguments, cancellationToken);
            var text = result is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : result as string;
            if (text is null)
            {
                return result;
            }

            if (text.StartsWith("Refused:", StringComparison.Ordinal))
            {
                throw new McpException(text);
            }

            return text;
        }
    }
}
