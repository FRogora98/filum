using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Filum.Mcp.Tests;

/// <summary>
/// The server as a host sees it (spec 013): started as its own process over stdio, on a temporary FILUM_HOME. A new
/// process is a new session. The client declares no sampling: nothing here may need it.
/// </summary>
public sealed class ServerTests : IDisposable
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
    public async Task A_new_session_gets_the_engines_catalog_and_the_instructions()
    {
        await using var client = await Session();

        var tools = await client.ListToolsAsync();
        var snapshot = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests", "Filum.Engine.Tests", "ToolCatalog.snapshot.json")))!.AsArray();

        Assert.Equal(28, tools.Count);
        Assert.Equal(snapshot.Select(t => (string)t!["name"]!).Append(McpServerSetup.LogTool).Append(McpServerSetup.ConsolidateTool).Order(), tools.Select(t => t.Name).Order());
        Assert.All(tools.Where(t => t.Name is not McpServerSetup.LogTool and not McpServerSetup.ConsolidateTool), tool =>
        {
            var expected = snapshot.Single(t => (string)t!["name"]! == tool.Name)!;
            Assert.Equal((string)expected["description"]!, tool.Description);
            Assert.True(JsonNode.DeepEquals(expected["parameters"], JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())), tool.Name);
        });
        Assert.Contains("memory_overview", client.ServerInstructions);
        Assert.Equal("filum", client.ServerInfo.Name);
    }

    [Fact]
    public async Task What_the_host_logs_is_kept_in_the_folders_log_changes_no_file_and_is_tidied_once()
    {
        await using var client = await Session();
        Assert.Equal("Nothing is waiting to be tidied.", Ok(await Call(client, McpServerSetup.ConsolidateTool, new())));

        var logged = Ok(await Call(client, McpServerSetup.LogTool, new() { ["text"] = "I moved to the coast in March.", ["occurredAt"] = "2026-03-02" }));
        Assert.StartsWith("Logged (event 1)", logged);
        Assert.True((await Call(client, McpServerSetup.LogTool, new() { ["text"] = "x", ["occurredAt"] = "someday" })).IsError);

        var log = File.ReadAllLines(Path.Combine(_home, ".filum", "events.jsonl"));
        var saved = JsonDocument.Parse(Assert.Single(log)).RootElement;
        Assert.Equal(("said", "host", "I moved to the coast in March."), (saved.GetProperty("kind").GetString(), saved.GetProperty("source").GetString(), saved.GetProperty("text").GetString()));
        Assert.StartsWith("2026-03-02", saved.GetProperty("occurredAt").GetString());
        Assert.False(File.Exists(Path.Combine(_home, "filum.md")));
        Assert.Contains("I moved to the coast in March.", Ok(await Call(client, "events_search", new() { ["query"] = "coast" })));

        var waiting = Ok(await Call(client, McpServerSetup.ConsolidateTool, new()));
        Assert.Contains("[event 1 · 2026-03-02", waiting);
        Assert.EndsWith("call memory_consolidate with through=1.", waiting);
        Assert.Equal("Done: 1 events tidied.", Ok(await Call(client, McpServerSetup.ConsolidateTool, new() { ["through"] = 1 })));
        Assert.Equal("Nothing is waiting to be tidied.", Ok(await Call(client, McpServerSetup.ConsolidateTool, new())));
        Assert.Contains("memory_log", client.ServerInstructions);
        Assert.Contains("memory_consolidate", client.ServerInstructions);
    }

    [Fact]
    public async Task A_fact_saved_in_one_session_is_found_in_the_next_and_is_a_plain_file()
    {
        await using (var first = await Session())
        {
            Ok(await Call(first, "memory_write", new() { ["path"] = "/notes/garden.md", ["content"] = "The lemon tree is watered on Sundays.\n" }));
        }

        await using var second = await Session();
        var found = Ok(await Call(second, "memory_search", new() { ["query"] = "lemon tree" }));

        Assert.Contains("/notes/garden.md", found);
        Assert.Equal("The lemon tree is watered on Sundays.\n", File.ReadAllText(Path.Combine(_home, "notes", "garden.md")));
    }

    [Fact]
    public async Task A_skill_saved_in_one_session_runs_in_the_next()
    {
        await using (var first = await Session())
        {
            Ok(await Call(first, "skill_save", new()
            {
                ["name"] = "tidy-notes",
                ["description"] = "Tidy the notes folder",
                ["when"] = "the person asks to tidy their notes",
                ["steps"] = "1. List the notes.\n2. Merge the duplicates."
            }));
        }

        await using var second = await Session();
        var steps = Ok(await Call(second, "skill_use", new() { ["name"] = "tidy-notes" }));

        Assert.Contains("Merge the duplicates", steps);
    }

    [Fact]
    public async Task A_refused_call_is_an_error_the_model_can_read_and_the_session_goes_on()
    {
        await using var client = await Session();

        var refused = await Call(client, "memory_write", new() { ["path"] = "/notes/../secret.md", ["content"] = "x" });
        Assert.True(refused.IsError);
        Assert.Contains("'..'", Text(refused));

        Ok(await Call(client, "memory_write", new() { ["path"] = "/notes/ok.md", ["content"] = "fine\n" }));
    }

    [Fact]
    public async Task More_calls_than_one_turn_allows_all_work_in_one_session()
    {
        await using var client = await Session();

        for (var i = 0; i < 25; i++)
        {
            Ok(await Call(client, "memory_write", new() { ["path"] = $"/notes/n{i}.md", ["content"] = $"note {i}\n" }));
        }
    }

    [Fact]
    public async Task A_file_edited_by_hand_between_calls_is_read_as_it_is_now()
    {
        await using var client = await Session();
        Ok(await Call(client, "memory_write", new() { ["path"] = "/notes/list.md", ["content"] = "one\n" }));

        File.WriteAllText(Path.Combine(_home, "notes", "list.md"), "one\ntwo\n");

        Assert.Contains("two", Ok(await Call(client, "memory_read", new() { ["path"] = "/notes/list.md" })));
        Assert.Contains("outside Filum", Ok(await Call(client, "memory_history", new() { ["path"] = "/notes/list.md" })));
    }

    [Fact]
    public async Task With_a_package_the_instructions_carry_its_rules_and_a_new_memory_its_files()
    {
        await using var client = await Session(Path.Combine(Root(), "packs", "example"));

        Assert.Contains("Keep the person's journal", client.ServerInstructions);
        Assert.StartsWith("# The rules of this assistant (package \"example\")", Ok(await Call(client, "memory_overview", [])));
        Assert.Equal("date,entry\n", File.ReadAllText(Path.Combine(_home, "journal.csv")));
    }

    [Fact]
    public async Task A_package_that_cannot_be_used_stops_the_server_with_its_problems()
    {
        var pack = Path.Combine(Path.GetDirectoryName(_home)!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pack);
        File.WriteAllText(Path.Combine(pack, "pack.json"), """{"format": 9, "name": "x", "version": "1", "description": "d"}""");
        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            start.ArgumentList.Add(Server);
            start.Environment["FILUM_HOME"] = _home;
            start.Environment["FILUM_PACK"] = pack;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)));

            Assert.Equal(1, process.ExitCode);
            Assert.Equal(string.Empty, await stdout);
            Assert.Contains("format 9", await stderr);
        }
        finally
        {
            Directory.Delete(pack, recursive: true);
        }
    }

    [Fact]
    public async Task A_folder_that_cannot_be_written_stops_the_server_with_a_message_and_no_protocol_output()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_home)!);
        File.WriteAllText(_home, "a file where the folder should be");
        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            start.ArgumentList.Add(Server);
            start.Environment["FILUM_HOME"] = _home;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)));

            Assert.Equal(1, process.ExitCode);
            Assert.Equal(string.Empty, await stdout);
            Assert.Contains("FILUM_HOME", await stderr);
        }
        finally
        {
            File.Delete(_home);
        }
    }

    private Task<McpClient> Session(string? pack = null) =>
        McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "filum",
            Command = "dotnet",
            Arguments = [Server],
            EnvironmentVariables = new Dictionary<string, string?> { ["FILUM_HOME"] = _home, ["FILUM_PACK"] = pack }
        }));

    private static async Task<CallToolResult> Call(McpClient client, string tool, Dictionary<string, object?> arguments) =>
        await client.CallToolAsync(tool, arguments);

    private static string Ok(CallToolResult result)
    {
        Assert.False(result.IsError == true, Text(result));
        return Text(result);
    }

    private static string Text(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static string Root()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("Filum.slnx not found above the test output.");
        }

        return folder;
    }
}
