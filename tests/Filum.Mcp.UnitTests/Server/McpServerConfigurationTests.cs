using Filum.Mcp.Configuration;
using Filum.Mcp.Server;
using Microsoft.Extensions.Options;
using Filum.Mcp.UnitTests.Builders;
using ModelContextProtocol.Server;
using Filum.Mcp.Tools.Contracts;

namespace Filum.Mcp.UnitTests.Server;

public sealed class McpServerConfigurationTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void Configure_WhenThereIsNoPackage_ShouldServeTheCatalogAndTheServersToolsWithTheInstructions()
    {
        var options = new McpServerOptions();
        IServerToolStrategy[] own = [_server.LogToolStrategy(), _server.ConsolidateToolStrategy(), _server.ImportToolStrategy()];

        new McpServerConfiguration(_server.Memory, _server.Catalog(), own, Options.Create(new McpOptions { Tools = "full" })).Configure(options);

        var names = options.ToolCollection!.Select(t => t.ProtocolTool.Name).Order();
        var catalog = _server.Catalog().Functions().Select(f => f.Name);
        Assert.Equal(catalog.Concat(["memory_log", "memory_consolidate", "memory_import"]).Order(), names);
        Assert.Equal("filum", options.ServerInfo!.Name);
        Assert.Equal(McpInstructions.Text, options.ServerInstructions);
    }

    [Fact]
    public void Configure_WhenNoProfileIsChosen_ShouldServeTheCoreToolsOnly()
    {
        var options = new McpServerOptions();
        IServerToolStrategy[] own = [_server.LogToolStrategy(), _server.ConsolidateToolStrategy(), _server.ImportToolStrategy()];

        new McpServerConfiguration(_server.Memory, _server.Catalog(), own, Options.Create(new McpOptions())).Configure(options);

        Assert.Equal(McpConstants.Tools.Core.Order(StringComparer.Ordinal), options.ToolCollection!.Select(t => t.ProtocolTool.Name).Order(StringComparer.Ordinal));
        // Spec 046: consolidation, the safety net of what a turn missed, and forgetting are in the core profile.
        Assert.Contains("memory_consolidate", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("memory_delete", options.ToolCollection!.Select(t => t.ProtocolTool.Name));
    }

    [Fact]
    public void Configure_WhenTheMemoryHoldsRules_ShouldEndTheInstructionsWithThemUnderClaudeCodesCut()
    {
        var options = new McpServerOptions();
        IServerToolStrategy[] own = [_server.LogToolStrategy(), _server.ConsolidateToolStrategy(), _server.ImportToolStrategy()];
        var rules = "# Rules the person set: follow every one in this answer\n\n" + string.Join('\n', Enumerable.Range(1, 30).Select(i => $"- rule number {i}, kept the same way"));
        _server.Memory.KeepRules(rules);

        new McpServerConfiguration(_server.Memory, _server.Catalog(), own, Options.Create(new McpOptions { Tools = "full" })).Configure(options);

        Assert.StartsWith(McpInstructions.Text, options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("\n\n# Rules the person set: follow every one in this answer\n\n- rule number 1, kept the same way", options.ServerInstructions, StringComparison.Ordinal);
        Assert.True(options.ServerInstructions!.Length < 4_096, $"{options.ServerInstructions.Length} characters");
    }
}
