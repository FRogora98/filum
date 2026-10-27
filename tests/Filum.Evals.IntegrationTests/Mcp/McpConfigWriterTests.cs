using Filum.Evals.Mcp;

namespace Filum.Evals.IntegrationTests.Mcp;

public sealed class McpConfigWriterTests
{
    [Fact]
    public async Task WriteConfigAsync_WhenTheHookIsOn_ShouldWriteSettingsWhoseHookLogsIntoTheRunsMemory()
    {
        var writer = new McpConfigWriter("C:/tools/filum-mcp.dll", hook: true);
        var root = Path.Combine(Path.GetTempPath(), $"filum-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var config = await writer.WriteConfigAsync(root, withFilum: true, Path.Combine(root, "memory"), CancellationToken.None);
            var settings = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, McpConfigWriter.SettingsFile)));
            var command = settings.RootElement.GetProperty("hooks").GetProperty("UserPromptSubmit")[0].GetProperty("hooks")[0].GetProperty("command").GetString();

            Assert.True(File.Exists(config));
            Assert.Equal($"dotnet \"C:/tools/filum-mcp.dll\" log --home \"{Path.Combine(root, "memory").Replace('\\', '/')}\"", command);

            // Without the hook, no settings: the agent runs as before.
            var plain = Directory.CreateDirectory(Path.Combine(root, "plain")).FullName;
            await new McpConfigWriter("filum-mcp").WriteConfigAsync(plain, withFilum: true, root, CancellationToken.None);
            Assert.False(File.Exists(Path.Combine(plain, McpConfigWriter.SettingsFile)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
