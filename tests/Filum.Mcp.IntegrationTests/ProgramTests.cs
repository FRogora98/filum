using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Filum.Mcp.IntegrationTests;

// A new process is a new session. The client declares no sampling: nothing here may need it.
public sealed class ProgramTests : IDisposable
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
    public async Task ListTools_WhenASessionStarts_ShouldGiveTheEnginesCatalogAndTheInstructions()
    {
        await using var client = await Session();

        var tools = await client.ListToolsAsync();
        var snapshot = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests", "Filum.Engine.IntegrationTests", "Snapshots", "ToolCatalog.snapshot.json")))!.AsArray();

        Assert.Equal(33, tools.Count);
        Assert.Equal(snapshot.Select(t => (string)t!["name"]!).Append(McpConstants.Tools.Log).Append(McpConstants.Tools.Consolidate).Append(McpConstants.Tools.Import).Order(), tools.Select(t => t.Name).Order());
        Assert.All(tools.Where(t => t.Name is not McpConstants.Tools.Log and not McpConstants.Tools.Consolidate and not McpConstants.Tools.Import), tool =>
        {
            var expected = snapshot.Single(t => (string)t!["name"]! == tool.Name)!;
            Assert.Equal((string)expected["description"]!, tool.Description);
            Assert.True(JsonNode.DeepEquals(expected["parameters"], JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())), tool.Name);
        });
        Assert.Contains("memory_overview", client.ServerInstructions);
        Assert.Equal("filum", client.ServerInfo.Name);
    }

    [Fact]
    public async Task MemoryLog_WhenTheHostLogs_ShouldKeepItInTheFoldersLogChangeNoFileAndBeTidiedOnce()
    {
        await using var client = await Session();
        Assert.Equal("Nothing is waiting to be tidied.", Ok(await Call(client, McpConstants.Tools.Consolidate, new())));

        var logged = Ok(await Call(client, McpConstants.Tools.Log, new() { ["text"] = "I moved to the coast in March.", ["occurredAt"] = "2026-03-02" }));
        Assert.StartsWith("Logged (event 1)", logged);
        Assert.True((await Call(client, McpConstants.Tools.Log, new() { ["text"] = "x", ["occurredAt"] = "someday" })).IsError);

        var log = File.ReadAllLines(Path.Combine(_home, ".filum", "events.jsonl"));
        var saved = JsonDocument.Parse(Assert.Single(log)).RootElement;
        Assert.Equal(("said", "host", "I moved to the coast in March."), (saved.GetProperty("kind").GetString(), saved.GetProperty("source").GetString(), saved.GetProperty("text").GetString()));
        Assert.StartsWith("2026-03-02", saved.GetProperty("occurredAt").GetString());
        Assert.False(File.Exists(Path.Combine(_home, "filum.md")));
        Assert.Contains("I moved to the coast in March.", Ok(await Call(client, "events_search", new() { ["query"] = "coast" })));

        var waiting = Ok(await Call(client, McpConstants.Tools.Consolidate, new()));
        Assert.Contains("[event 1 · 2026-03-02", waiting);
        Assert.EndsWith("call memory_consolidate with through=1.", waiting);
        Assert.Equal("Done: 1 events tidied.", Ok(await Call(client, McpConstants.Tools.Consolidate, new() { ["through"] = 1 })));
        Assert.Equal("Nothing is waiting to be tidied.", Ok(await Call(client, McpConstants.Tools.Consolidate, new())));
        Assert.Contains("memory_log", client.ServerInstructions);
        Assert.Contains("memory_consolidate", client.ServerInstructions);
    }

    [Fact]
    public async Task MemoryImport_WhenAFileIsPreviewedThenImported_ShouldRecordItInTheFoldersLogAndHandItToTheHostToTidy()
    {
        var file = Path.Combine(Path.GetTempPath(), "filum-mcp-tests", $"cal-{Guid.NewGuid():N}.ics");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x1\r\nDTSTART:20240320T143000Z\r\nSUMMARY:Planning meeting\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
        try
        {
            await using var client = await Session();

            var preview = Ok(await Call(client, McpConstants.Tools.Import, new() { ["path"] = file }));
            Assert.StartsWith("Preview, nothing written. ics: 1 items, 1 new", preview);
            Assert.Contains("- Calendar: Planning meeting · 2024-03-20 14:30", preview);
            Assert.False(File.Exists(Path.Combine(_home, ".filum", "events.jsonl")));

            var imported = Ok(await Call(client, McpConstants.Tools.Import, new() { ["path"] = file, ["preview"] = false }));
            Assert.StartsWith("Imported 1 items.", imported);
            Assert.Contains("\"kind\":\"imported\"", File.ReadAllText(Path.Combine(_home, ".filum", "events.jsonl")));
            Assert.Contains("Calendar: Planning meeting", Ok(await Call(client, McpConstants.Tools.Consolidate, new())));
            Assert.True((await Call(client, McpConstants.Tools.Import, new() { ["path"] = file + ".missing" })).IsError);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task EventsSearch_WhenTheSearchModelIsOff_ShouldDownloadNothingAndSearchByWords()
    {
        await using var client = await Session();

        Ok(await Call(client, McpConstants.Tools.Log, new() { ["text"] = "The spare key is under the blue pot." }));
        Assert.Contains("blue pot", Ok(await Call(client, "events_search", new() { ["query"] = "spare key" })));
        Assert.False(Directory.Exists(_home + "-models"));
    }

    [Fact]
    public async Task MemoryWrite_WhenAFactIsSavedInOneSession_ShouldFindItInTheNextAsAPlainFile()
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
    public async Task SkillSave_WhenSavedInOneSession_ShouldRunInTheNext()
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
    public async Task CallTool_WhenTheCallIsRefused_ShouldReturnAnErrorTheModelCanReadAndGoOn()
    {
        await using var client = await Session();

        var refused = await Call(client, "memory_write", new() { ["path"] = "/notes/../secret.md", ["content"] = "x" });
        Assert.True(refused.IsError);
        Assert.Contains("'..'", Text(refused));

        Ok(await Call(client, "memory_write", new() { ["path"] = "/notes/ok.md", ["content"] = "fine\n" }));
    }

    [Fact]
    public async Task CallTool_WhenThereAreMoreCallsThanOneTurnAllows_ShouldAllWorkInOneSession()
    {
        await using var client = await Session();

        for (var i = 0; i < 25; i++)
        {
            Ok(await Call(client, "memory_write", new() { ["path"] = $"/notes/n{i}.md", ["content"] = $"note {i}\n" }));
        }
    }

    [Fact]
    public async Task MemoryRead_WhenTheFileWasEditedByHandBetweenCalls_ShouldReadItAsItIsNow()
    {
        await using var client = await Session();
        Ok(await Call(client, "memory_write", new() { ["path"] = "/notes/list.md", ["content"] = "one\n" }));

        File.WriteAllText(Path.Combine(_home, "notes", "list.md"), "one\ntwo\n");

        Assert.Contains("two", Ok(await Call(client, "memory_read", new() { ["path"] = "/notes/list.md" })));
        Assert.Contains("outside Filum", Ok(await Call(client, "memory_history", new() { ["path"] = "/notes/list.md" })));
    }

    [Fact]
    public async Task Start_WhenGivenAPackage_ShouldCarryItsRulesInTheInstructionsAndItsFilesInANewMemory()
    {
        await using var client = await Session(Path.Combine(Root(), "packs", "example"));

        Assert.Contains("Keep the person's journal", client.ServerInstructions);
        Assert.StartsWith("# The rules of this assistant (package \"example\")", Ok(await Call(client, "memory_overview", [])));
        Assert.Equal("date,entry\n", File.ReadAllText(Path.Combine(_home, "journal.csv")));
    }

    [Fact]
    public async Task Start_WhenThePackageCannotBeUsed_ShouldStopTheServerWithItsProblems()
    {
        var pack = Path.Combine(Path.GetDirectoryName(_home)!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pack);
        File.WriteAllText(Path.Combine(pack, "pack.json"), """{"format": 9, "name": "x", "version": "1", "description": "d"}""");
        try
        {
            var (exitCode, stdout, stderr) = await StartAndWaitAsync(new() { ["FILUM_HOME"] = _home, ["FILUM_EMBEDDINGS"] = "off", ["FILUM_PACK"] = pack });

            Assert.Equal((1, string.Empty), (exitCode, stdout));
            Assert.Contains("format 9", stderr);
        }
        finally
        {
            Directory.Delete(pack, recursive: true);
        }
    }

    [Fact]
    public async Task Start_WhenTheFolderCannotBeWritten_ShouldStopTheServerWithAMessageAndNoProtocolOutput()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_home)!);
        File.WriteAllText(_home, "a file where the folder should be");
        try
        {
            var (exitCode, stdout, stderr) = await StartAndWaitAsync(new() { ["FILUM_HOME"] = _home, ["FILUM_EMBEDDINGS"] = "off" });

            Assert.Equal((1, string.Empty), (exitCode, stdout));
            Assert.Contains("FILUM_HOME", stderr);
        }
        finally
        {
            File.Delete(_home);
        }
    }

    [Fact]
    public async Task Start_WhenASettingIsNotAFolderPath_ShouldStopTheServerBeforeAnyProtocolOutput()
    {
        var (exitCode, stdout, stderr) = await StartAndWaitAsync(new() { ["FILUM_HOME"] = "   ", ["FILUM_EMBEDDINGS"] = "off" });

        Assert.Equal((1, string.Empty), (exitCode, stdout));
        Assert.Contains("FILUM_HOME is not a folder path", stderr);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> StartAndWaitAsync(Dictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        start.ArgumentList.Add(Server);
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)));
        return (process.ExitCode, await stdout, await stderr);
    }

    private Task<McpClient> Session(string? pack = null) =>
        McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "filum",
            Command = "dotnet",
            Arguments = [Server],
            EnvironmentVariables = new Dictionary<string, string?> { ["FILUM_HOME"] = _home, ["FILUM_PACK"] = pack, ["FILUM_EMBEDDINGS"] = "off", ["FILUM_MODELS"] = _home + "-models", ["FILUM_TOOLS"] = "full" }
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
