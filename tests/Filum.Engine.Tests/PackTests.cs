using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Filum.Engine.Tests;

/// <summary>Packages (spec 016): the format, the check, and a new memory made from one.</summary>
public sealed class PackTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly MemoryOptions Limits = new();

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "filum-pack-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _user = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Fact]
    public void The_example_package_loads()
    {
        var pack = Example();

        Assert.Equal("example", pack.Name);
        Assert.Equal("1.0.0", pack.Version);
        Assert.Contains("/journal.csv", pack.Prompt);
        Assert.Null(pack.Core);
        Assert.Equal(["weekly-review"], pack.Skills.Select(s => s.Name));
        Assert.Equal(["/about-this-pack.md", "/journal.csv"], pack.Files.Select(f => f.Path).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_new_memory_starts_with_the_package(bool onAFolder)
    {
        var memory = Service(onAFolder ? new LocalFolderStore(_temp, _user) : new InMemoryMemoryStore(), Example());

        var core = await memory.EnsureCoreAsync(_user, None);

        Assert.Equal(PlatformInstructions.CoreTemplate, core);
        var journal = Ok(await memory.GetFileAsync(_user, "/journal.csv", None));
        Assert.Equal("date,entry\n", journal.Content);
        Assert.Equal(MemorySensitivity.Sensitive, journal.Info.Sensitivity);
        Assert.Equal(MemorySensitivity.Normal, Ok(await memory.GetFileAsync(_user, "/about-this-pack.md", None)).Info.Sensitivity);
        Assert.Equal(
            Skills.Starters.Select(s => s.Name).Append("weekly-review").Order(),
            (await memory.ListSkillsAsync(_user, includePrivate: true, None)).Select(s => s.Skill.Name).Order());
        Assert.All(Ok(await memory.GetHistoryAsync(_user, "/journal.csv", None)), r => Assert.Equal(MemoryAuthor.Platform, r.Author));
    }

    [Fact]
    public async Task Rows_added_later_keep_the_sensitivity_the_package_declared()
    {
        var memory = Service(new InMemoryMemoryStore(), Example());
        await memory.EnsureCoreAsync(_user, None);

        Ok(await memory.AddRowsAsync(_user, MemoryActor.Person, "/journal.csv", [new Dictionary<string, string> { ["date"] = "2026-01-05", ["entry"] = "A quiet day." }], None));

        Assert.Equal(MemorySensitivity.Sensitive, Ok(await memory.GetFileAsync(_user, "/journal.csv", None)).Info.Sensitivity);
    }

    [Fact]
    public async Task Without_a_package_a_new_memory_is_as_before()
    {
        var memory = Service(new InMemoryMemoryStore(), pack: null);

        Assert.Equal(PlatformInstructions.CoreTemplate, await memory.EnsureCoreAsync(_user, None));
        Assert.Equal(Skills.Starters.Select(s => s.Name).Order(), (await memory.ListSkillsAsync(_user, includePrivate: true, None)).Select(s => s.Skill.Name).Order());
        Assert.Equal(1 + Skills.Starters.Count, Ok(await memory.ListAsync(_user, "/", includePrivate: true, None)).Count);
        Assert.Equal(string.Empty, PlatformInstructions.PackSection(memory.Pack));
    }

    [Fact]
    public async Task A_package_core_replaces_the_template_and_a_package_skill_replaces_a_starter_of_its_name()
    {
        var folder = Package(new()
        {
            ["memory/filum.md"] = "# About you\n\n# Rules\n- Answer in short sentences.\n\n# How to answer\n\n# Memory map\n",
            ["skills/what-you-know-about-me.md"] = Skills.Format(new Skill("what-you-know-about-me", "A one-line summary", "the person asks what you know", true, "1. Say it in one line.")),
        });
        var memory = Service(new InMemoryMemoryStore(), Pack.Load(folder, Limits));

        Assert.Contains("Answer in short sentences.", await memory.EnsureCoreAsync(_user, None));
        var skills = await memory.ListSkillsAsync(_user, includePrivate: true, None);
        Assert.Equal(Skills.Starters.Count, skills.Count);
        Assert.Equal("A one-line summary", skills.Single(s => s.Skill.Name == "what-you-know-about-me").Skill.Description);
    }

    [Fact]
    public void A_package_with_problems_is_refused_with_every_problem_listed()
    {
        var folder = Package(new()
        {
            ["skills/broken.md"] = "no header here\n",
            ["memory/notes.txt"] = "not a memory file\n",
            ["memory/empty.csv"] = string.Empty,
        });

        var refused = Assert.Throws<PackException>(() => Pack.Load(folder, Limits));

        Assert.Equal(3, refused.Problems.Count);
        Assert.Contains(refused.Problems, p => p.Contains("skills/broken.md"));
        Assert.Contains(refused.Problems, p => p.Contains("/notes.txt"));
        Assert.Contains(refused.Problems, p => p.Contains("/empty.csv"));
    }

    [Theory]
    [InlineData("""{"format": 2, "name": "x", "version": "1", "description": "d"}""", "format 2")]
    [InlineData("""{"format": 1, "name": "x", "version": "1", "description": "d", "tools": []}""", "'tools'")]
    [InlineData("""{"format": 1, "name": "Not Valid", "version": "1", "description": "d"}""", "lowercase")]
    [InlineData("""{"format": 1, "name": "x", "version": "1", "description": "d", "private": ["/missing.md"]}""", "/missing.md")]
    [InlineData("not json", "not valid JSON")]
    public void A_manifest_the_engine_does_not_understand_is_refused(string manifest, string expected)
    {
        var folder = Package([], manifest);

        Assert.Contains(Assert.Throws<PackException>(() => Pack.Load(folder, Limits)).Problems, p => p.Contains(expected));
    }

    [Fact]
    public void A_missing_folder_is_refused() =>
        Assert.Throws<PackException>(() => Pack.Load(Path.Combine(_temp, "nowhere"), Limits));

    [Fact]
    public void The_prompt_comes_after_the_platform_layer_and_before_the_core_which_never_overrides_it()
    {
        var text = PlatformInstructions.Compose("CORE-TEXT", "INDEX", "SKILLS", pack: Example());

        var platform = text.IndexOf(PlatformInstructions.Text, StringComparison.Ordinal);
        var prompt = text.IndexOf("Keep the person's journal", StringComparison.Ordinal);
        var core = text.IndexOf("CORE-TEXT", StringComparison.Ordinal);
        Assert.True(platform == 0 && platform < prompt && prompt < core);
        Assert.Contains("the person's core adds to them and never overrides them", text[..core]);
    }

    [Fact]
    public async Task A_memory_made_before_the_package_keeps_its_files_and_gets_the_prompt()
    {
        var store = new InMemoryMemoryStore();
        await Service(store, pack: null).EnsureCoreAsync(_user, None);

        var withPack = Service(store, Example());
        await withPack.EnsureCoreAsync(_user, None);

        Assert.True((await withPack.GetFileAsync(_user, "/journal.csv", None)).IsMissing);
        Assert.Contains("Keep the person's journal", PlatformInstructions.PackSection(withPack.Pack));
    }

    [Fact]
    public async Task The_overview_starts_with_the_package_rules()
    {
        var memory = Service(new InMemoryMemoryStore(), Example());
        var tools = new MemoryTools(memory, Limits, _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));

        var result = await tools.Tools.OfType<AIFunction>().Single(t => t.Name == "memory_overview").InvokeAsync(new AIFunctionArguments());
        var text = result is JsonElement json ? json.GetString()! : (string)result!;

        Assert.StartsWith("# The rules of this assistant (package \"example\")", text);
    }

    private static MemoryService Service(IMemoryStore store, Pack? pack) =>
        new(store, Options.Create(Limits), NullLogger<MemoryService>.Instance, pack: pack);

    private static Pack Example() => Pack.Load(Path.Combine(Root(), "packs", "example"), Limits);

    private string Package(Dictionary<string, string> files, string manifest = """{"format": 1, "name": "test-pack", "version": "1.0.0", "description": "A test package."}""")
    {
        var folder = Path.Combine(_temp, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pack.json"), manifest);
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return folder;
    }

    private static T Ok<T>(MemoryOutcome<T> outcome) where T : class
    {
        Assert.False(outcome.IsRefused, outcome.Refusal);
        return outcome.Value!;
    }

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
