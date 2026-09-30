using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Filum.Engine.Tests;

/// <summary>The tool surface (spec 011): one catalog, reviewed through a snapshot, and the tools an external host needs.</summary>
public sealed class ToolCatalogTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryActor Agent = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());

    private readonly Guid _user = Guid.NewGuid();
    private readonly MemoryService _memory = new(new InMemoryMemoryStore(), Options.Create(new MemoryOptions()), NullLogger<MemoryService>.Instance);

    [Fact]
    public void The_catalog_matches_its_snapshot()
    {
        var catalog = new JsonArray(MemoryTools.Catalog(new MemoryOptions())
            .Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["parameters"] = JsonNode.Parse(t.JsonSchema.GetRawText())
            })
            .ToArray());
        var text = catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";
        var snapshot = Path.Combine(SourceFolder(), "ToolCatalog.snapshot.json");

        // A deliberate change to the surface: FILUM_UPDATE_SNAPSHOT=1 dotnet test, then review the diff.
        if (Environment.GetEnvironmentVariable("FILUM_UPDATE_SNAPSHOT") == "1")
        {
            File.WriteAllText(snapshot, text);
        }

        Assert.True(File.Exists(snapshot), "No snapshot yet: run once with FILUM_UPDATE_SNAPSHOT=1 and review it.");
        Assert.Equal(File.ReadAllText(snapshot).Replace("\r\n", "\n"), text);
    }

    [Fact]
    public void Every_tool_has_a_name_a_description_and_typed_parameters()
    {
        var catalog = MemoryTools.Catalog(new MemoryOptions());

        Assert.Equal(21, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Select(t => t.Name).Distinct().Count());
        Assert.All(catalog, t =>
        {
            Assert.Matches("^(memory|collection|skill)_[a-z_]+$", t.Name);
            Assert.True(t.Description.Length > 40, t.Name);
            Assert.Equal("object", t.JsonSchema.GetProperty("type").GetString());
        });
        Assert.Contains(catalog, t => t.Name == "memory_overview");
        Assert.Contains(catalog, t => t.Name == "skill_list");
        Assert.Contains(catalog, t => t.Name == "memory_history");
        Assert.Contains(catalog, t => t.Name == "memory_undo");
    }

    [Fact]
    public async Task A_host_leaves_tools_out_and_they_cannot_be_called()
    {
        var limits = new MemoryOptions { ExcludedTools = ["memory_delete", "collection_remove_rows"] };
        var tools = new MemoryTools(_memory, limits, _user, Agent);

        Assert.Equal(MemoryTools.Catalog(new MemoryOptions()).Select(t => t.Name).Except(["memory_delete", "collection_remove_rows"]), tools.Tools.Select(t => t.Name));
        Assert.Equal(21, new MemoryTools(_memory, new MemoryOptions(), _user, Agent).Tools.Count);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_overview_gives_the_core_the_index_and_the_enabled_public_skills()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/plans.md", "one\ntwo\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/secret.md", "hidden\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/secret.md", MemorySensitivity.Private, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("log-note", "Keep a note", "the person says note that", true, "1. Add it."), false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("old-way", "An old way", "never", true, "1. Nothing."), false, None));
        Ok(await _memory.SetSkillEnabledAsync(_user, Agent, "old-way", false, None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), _user, Agent);

        var overview = await Call(tools, "memory_overview", []);

        Assert.Contains(PlatformInstructions.CoreTemplate, overview);
        Assert.Contains(await _memory.BuildIndexAsync(_user, None), overview);
        Assert.Contains("- /notes/plans.md (document · 2 lines", overview);
        Assert.DoesNotContain("secret", overview);
        Assert.Contains("- /log-note: Keep a note · when the person says note that", overview);
        Assert.DoesNotContain("old-way", overview);
        Assert.Equal((ToolStep.Read, "Looked at the memory"), (tools.Steps[0].Kind, tools.Steps[0].Description));
    }

    [Fact]
    public async Task Skill_list_gives_every_skill_with_its_state_and_private_ones_only_when_asked()
    {
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("log-note", "Keep a note", "note that", true, "1. Add it."), false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("old-way", "An old way", "never", true, "1. Nothing."), false, None));
        Ok(await _memory.SetSkillEnabledAsync(_user, Agent, "old-way", false, None));
        Ok(await _memory.SaveSkillAsync(_user, Agent, new Skill("hidden-one", "Hidden", "rarely", true, "1. Shh."), false, None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/skills/hidden-one.md", MemorySensitivity.Private, None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), _user, Agent);

        var listed = await Call(tools, "skill_list", []);
        var all = await Call(tools, "skill_list", new() { ["includePrivate"] = true });

        Assert.Equal(["- /log-note: Keep a note · when note that · on", "- /old-way: An old way · when never · off"], listed.Split('\n'));
        Assert.Contains("- /hidden-one: Hidden · when rarely · on · private", all);
    }

    [Fact]
    public async Task History_lists_the_changes_and_undo_takes_back_the_latest_only()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "first\n", None));
        var second = Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "second", None));
        var tools = new MemoryTools(_memory, new MemoryOptions(), _user, Agent);

        var history = (await Call(tools, "memory_history", new() { ["path"] = "/notes/a.md" })).Split('\n');
        var first = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None))[1].Id;
        var refused = await Call(tools, "memory_undo", new() { ["revisionId"] = first });
        var undone = await Call(tools, "memory_undo", new() { ["revisionId"] = second.RevisionId });

        Assert.Equal(2, history.Length);
        Assert.StartsWith($"- change {second.RevisionId}: Added 1 line · by the person · ", history[0]);
        Assert.StartsWith($"- change {first}: Created · by the agent · ", history[1]);
        Assert.StartsWith("Refused: /notes/a.md changed again after that", refused);
        Assert.StartsWith($"Undid change {second.RevisionId}", undone);
        Assert.Equal(["first"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        Assert.Equal(
            [(ToolStep.Read, "Read the history of /notes/a.md"), (ToolStep.Failed, $"Could not undo change {first}"), (ToolStep.Wrote, "Undid a change to /notes/a.md")],
            tools.Steps.Select(s => (s.Kind, s.Description)));
        Assert.Single(tools.Revisions);
    }

    private static async Task<string> Call(MemoryTools tools, string name, Dictionary<string, object?> arguments)
    {
        var function = (AIFunction)tools.Tools.Single(t => t.Name == name);
        return (await function.InvokeAsync(new AIFunctionArguments(arguments)))?.ToString() ?? string.Empty;
    }

    private static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.False(outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }

    private static string SourceFolder()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "Filum.Engine.Tests.csproj")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("The test project folder was not found.");
        }

        return folder;
    }
}
