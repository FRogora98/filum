using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Instructions;
using Filum.Mcp.Session;
using Filum.Mcp.Tools;
using Filum.Mcp.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.Server;

internal sealed class McpServerConfiguration(OpenedMemory memory, EngineToolCatalog catalog, IEnumerable<IServerToolStrategy> serverTools, IOptions<McpOptions> settings) : IConfigureOptions<McpServerOptions>
{
    private const string UnknownVersion = "0.0.0";

    private const int VersionFields = 3;

    private const string RulesSeparator = "\n\n";

    public void Configure(McpServerOptions options)
    {
        options.ServerInfo = new Implementation { Name = McpConstants.ServerName, Version = Version() };
        var tools = catalog.Functions().Concat(serverTools.Select(t => new PlainTextFunction(t.Function()))).Where(t => settings.Value.Offers(t.Name)).ToList();
        options.ServerInstructions = Instructions(tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal));
        options.ToolCollection ??= [];
        foreach (var tool in tools)
        {
            var created = Annotated(ToolTraits.Of(tool.Name) ?? McpConstants.Tools.ServerTool);
            created.OutputSchema = OutputSchemas.Of(tool.Name);
            created.UseStructuredContent = created.OutputSchema is not null;
            options.ToolCollection.Add(McpServerTool.Create(tool, created));
        }
    }

    // The engine's table of what each tool does (spec 040); no tool reaches outside the memory.
    private static McpServerToolCreateOptions Annotated(ToolTraits traits) =>
        new() { ReadOnly = traits.ReadOnly, Destructive = traits.Destructive, Idempotent = traits.Idempotent, OpenWorld = false };

    // The rules last, in the room left under Claude Code's cut (spec 056).
    private string Instructions(IReadOnlySet<string> offered)
    {
        var text = McpInstructions.For(offered) + (memory.Pack?.Prompt is null ? string.Empty : "\n" + PlatformInstructions.PackSection(memory.Pack));
        var rules = McpInstructions.Fit(memory.Rules, McpConstants.Limits.MaxInstructions - text.Length - RulesSeparator.Length);
        return rules.Length == 0 ? text : text + RulesSeparator + rules;
    }

    private static string Version() => typeof(McpServerConfiguration).Assembly.GetName().Version?.ToString(VersionFields) ?? UnknownVersion;
}
