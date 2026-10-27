using Filum.Evals.Mcp;
using System.Text.Json;

namespace Filum.Evals.UnitTests.Mcp;

public sealed class McpConfigWriterTests
{
    [Fact]
    public void McpConfig_WhenWithOrWithoutFilum_ShouldMountOnlyFilumMcpOnItsFolderOrNothing()
    {
        var writer = new McpConfigWriter("/tools/filum-mcp.dll");

        var with = JsonDocument.Parse(writer.McpConfig(withFilum: true, "/tmp/home")).RootElement.GetProperty("mcpServers");
        var without = JsonDocument.Parse(writer.McpConfig(withFilum: false, "/tmp/home")).RootElement.GetProperty("mcpServers");

        Assert.Equal(["filum"], with.EnumerateObject().Select(p => p.Name));
        Assert.Equal("dotnet", with.GetProperty("filum").GetProperty("command").GetString());
        Assert.Equal("/tools/filum-mcp.dll", with.GetProperty("filum").GetProperty("args")[0].GetString());
        Assert.Equal("/tmp/home", with.GetProperty("filum").GetProperty("env").GetProperty("FILUM_HOME").GetString());
        Assert.Equal("full", with.GetProperty("filum").GetProperty("env").GetProperty("FILUM_TOOLS").GetString());
        Assert.Empty(without.EnumerateObject());
    }
}
