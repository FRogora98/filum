using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Filum.Mcp.IntegrationTests;

// Spec 040: what a host reads besides the text: annotations, typed errors, structured content.
public sealed class InterfaceTests : IDisposable
{
    private static readonly string Server = Path.Combine(AppContext.BaseDirectory, "filum-mcp.dll");

    private readonly string _home = Path.Combine(Path.GetTempPath(), "filum-mcp-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact]
    public async Task ListTools_WhenListed_ShouldCarryTheAnnotationsOfEachTool()
    {
        await using var client = await Session();

        var tools = (await client.ListToolsAsync()).ToDictionary(t => t.Name, t => t.ProtocolTool.Annotations);

        Assert.All(tools, t => Assert.NotNull(t.Value));
        Assert.All(tools, t => Assert.False(t.Value!.OpenWorldHint, t.Key));
        Assert.Equal((true, false, true), (tools["facts_current"]!.ReadOnlyHint, tools["facts_current"]!.DestructiveHint, tools["facts_current"]!.IdempotentHint));
        Assert.Equal((false, true), (tools["memory_delete"]!.ReadOnlyHint, tools["memory_delete"]!.DestructiveHint));
        Assert.Equal((false, false), (tools["memory_append"]!.ReadOnlyHint, tools["memory_append"]!.DestructiveHint));
    }

    [Fact]
    public async Task CallTool_WhenTheEngineRefusesTheCall_ShouldGiveAToolErrorWithTheReason()
    {
        await using var client = await Session();

        var refused = await client.CallToolAsync("memory_read", new Dictionary<string, object?> { ["path"] = "/notes/missing.md" });

        Assert.True(refused.IsError);
        Assert.Contains("/notes/missing.md", string.Join("\n", refused.Content.OfType<TextContentBlock>().Select(c => c.Text)));
    }

    [Fact]
    public async Task CallTool_WhenFactsAreRead_ShouldGiveThemAsStructuredContentToo()
    {
        await using var client = await Session();
        await client.CallToolAsync("fact_record", new Dictionary<string, object?> { ["subject"] = "Sam", ["attribute"] = "city", ["value"] = "Turin", ["validFrom"] = "2026-01-01" });
        await client.CallToolAsync("fact_record", new Dictionary<string, object?> { ["subject"] = "Sam", ["attribute"] = "city", ["value"] = "Lyon", ["validFrom"] = "2026-06-01" });

        var current = await client.CallToolAsync("facts_current", new Dictionary<string, object?> { ["subject"] = "Sam" });
        var history = await client.CallToolAsync("facts_history", new Dictionary<string, object?> { ["subject"] = "Sam" });

        var now = Assert.Single(current.StructuredContent!.Value.GetProperty("facts").EnumerateArray());
        Assert.Equal(("Sam", "city", "Lyon", "2026-06-01", ""), (now.GetProperty("subject").GetString(), now.GetProperty("attribute").GetString(), now.GetProperty("value").GetString(), now.GetProperty("from").GetString(), now.GetProperty("to").GetString()));
        Assert.Equal(["Turin", "Lyon"], history.StructuredContent!.Value.GetProperty("facts").EnumerateArray().Select(f => f.GetProperty("value").GetString()));
        var tools = (await client.ListToolsAsync()).ToDictionary(t => t.Name, t => t.ProtocolTool.OutputSchema);
        Assert.NotNull(tools["facts_current"]);
        Assert.NotNull(tools["facts_history"]);
        Assert.NotNull(tools["events_search"]);
        Assert.Null(tools["memory_read"]);
    }

    [Fact]
    public async Task CallTool_WhenWhatWasSaidIsSearched_ShouldGiveTheMatchesAsStructuredContentToo()
    {
        await using var client = await Session();
        await client.CallToolAsync("memory_log", new Dictionary<string, object?> { ["text"] = "The spare key is under the blue pot.", ["occurredAt"] = "2026-03-02" });

        var found = await client.CallToolAsync("events_search", new Dictionary<string, object?> { ["query"] = "spare key" });

        var match = Assert.Single(found.StructuredContent!.Value.GetProperty("matches").EnumerateArray());
        Assert.Equal(1, match.GetProperty("id").GetInt64());
        Assert.Equal("2026-03-02", match.GetProperty("date").GetString());
        Assert.Contains("blue pot", match.GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListTools_WhenNoProfileIsSet_ShouldGiveTheCoreToolsAndInstructionsThatNameNoOther()
    {
        await using var client = await Session(tools: null);

        var tools = await client.ListToolsAsync();
        var names = tools.Select(t => t.Name).Order(StringComparer.Ordinal);

        Assert.Equal(
            ["events_read", "events_search", "fact_end", "fact_record", "facts_current", "memory_append", "memory_consolidate", "memory_delete", "memory_edit", "memory_log", "memory_overview", "memory_read", "memory_search", "memory_write"],
            names);
        Assert.Contains("events_read", client.ServerInstructions);
        Assert.Contains("memory_log", client.ServerInstructions);
        Assert.Contains("memory_consolidate", client.ServerInstructions);
        // Claude Code cuts server instructions at 4,096 characters (spec 045): what is past it never reaches the model.
        Assert.True(client.ServerInstructions!.Length < 4_096, $"{client.ServerInstructions.Length} characters");
        Assert.Contains(Filum.Engine.Instructions.InstructionParts.Durable, client.ServerInstructions);
        Assert.DoesNotContain("skill_use", client.ServerInstructions);
        Assert.DoesNotContain("memory_undo", client.ServerInstructions);
        Assert.DoesNotContain("collection_add_rows", client.ServerInstructions);
        // The core profile's own budget (spec 041): names, descriptions and schemas, at most 7,000 characters.
        var size = tools.Sum(t => System.Text.Json.JsonSerializer.Serialize(new { t.Name, t.Description, t.JsonSchema }).Length);
        Assert.True(size <= 7_000, $"{size} characters");
    }

    [Fact]
    public async Task ListTools_WhenTheFullProfileIsSet_ShouldGiveEveryToolAndTheWholeInstructions()
    {
        await using var client = await Session(tools: "full");

        Assert.True(client.ServerInstructions!.Length < 4_096, $"{client.ServerInstructions.Length} characters");

        Assert.Equal(33, (await client.ListToolsAsync()).Count);
        Assert.Contains("memory_consolidate", client.ServerInstructions);
        Assert.Contains("skill_use", client.ServerInstructions);
    }

    [Fact]
    public async Task Instructions_WhenTheMemoryHoldsARule_ShouldCarryItUnlessTheFactsArePrivate()
    {
        using (var store = new Filum.Engine.Storage.LocalFolder.LocalFolderStore(_home))
        using (var engine = Filum.Engine.FilumEngine.Create(store))
        {
            await engine.Memory.RecordFactAsync(store.Owner, Filum.Engine.Abstractions.Memory.MemoryActor.Person, "the person", "rule", "end every answer with 'Onward.'", null, null, [], CancellationToken.None);
        }

        await using (var client = await Session())
        {
            Assert.Contains("- end every answer with 'Onward.'", client.ServerInstructions);
            Assert.True(client.ServerInstructions!.Length < 4_096, $"{client.ServerInstructions.Length} characters");
        }

        using (var store = new Filum.Engine.Storage.LocalFolder.LocalFolderStore(_home))
        using (var engine = Filum.Engine.FilumEngine.Create(store))
        {
            await engine.Memory.SetSensitivityAsync(store.Owner, Filum.Engine.Abstractions.Memory.MemoryActor.Person, "/facts.csv", Filum.Engine.Abstractions.Constants.MemorySensitivity.Private, CancellationToken.None);
        }

        await using var later = await Session();
        Assert.DoesNotContain("Onward", later.ServerInstructions);
    }

    private Task<McpClient> Session(string? tools = "full") =>
        McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "filum",
            Command = "dotnet",
            Arguments = [Server],
            EnvironmentVariables = new Dictionary<string, string?> { ["FILUM_HOME"] = _home, ["FILUM_EMBEDDINGS"] = "off", ["FILUM_MODELS"] = _home + "-models", ["FILUM_TOOLS"] = tools }
        }));
}
