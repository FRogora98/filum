using System.Text.Json;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Instructions;
using Filum.Engine.Memory;
using Filum.Engine.Packs;
using Filum.Engine.Skills;
using Filum.Engine.Storage.InMemory;
using Filum.Engine.Storage.LocalFolder;
using Filum.Engine.Testing.Builders;
using Filum.Engine.Tools;
using Microsoft.Extensions.AI;
using static Filum.Engine.Testing.Builders.Outcomes;
using Filum.Engine.Collections.Compute.Jint;

namespace Filum.Engine.IntegrationTests.Packs;

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
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_temp);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenGivenTheExamplePackage_ShouldLoadIt()
    {
        var pack = await ExampleAsync();

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
    public async Task EnsureCoreAsync_WhenTheMemoryIsNew_ShouldStartWithThePackage(bool onAFolder)
    {
        var memory = Service(onAFolder ? new LocalFolderStore(_temp, _user) : new InMemoryMemoryStore(), await ExampleAsync());

        var core = await memory.EnsureCoreAsync(_user, None);

        Assert.Equal(PlatformInstructions.CoreTemplate, core);
        var journal = Ok(await memory.GetFileAsync(_user, "/journal.csv", None));
        Assert.Equal("date,entry\n", journal.Content);
        Assert.Equal(MemorySensitivity.Sensitive, journal.Info.Sensitivity);
        Assert.Equal(MemorySensitivity.Normal, Ok(await memory.GetFileAsync(_user, "/about-this-pack.md", None)).Info.Sensitivity);
        Assert.Equal(
            StarterSkills.All.Select(s => s.Name).Append("weekly-review").Order(),
            (await memory.ListSkillsAsync(_user, includePrivate: true, None)).Select(s => s.Skill.Name).Order());
        Assert.All(Ok(await memory.GetHistoryAsync(_user, "/journal.csv", None)), r => Assert.Equal(MemoryAuthor.Platform, r.Author));
    }

    [Fact]
    public async Task AddRowsAsync_WhenThePackageDeclaredASensitivity_ShouldKeepIt()
    {
        var memory = Service(new InMemoryMemoryStore(), await ExampleAsync());
        await memory.EnsureCoreAsync(_user, None);

        Ok(await memory.AddRowsAsync(_user, MemoryActor.Person, "/journal.csv", [new Dictionary<string, string> { ["date"] = "2026-01-05", ["entry"] = "A quiet day." }], None));

        Assert.Equal(MemorySensitivity.Sensitive, Ok(await memory.GetFileAsync(_user, "/journal.csv", None)).Info.Sensitivity);
    }

    [Fact]
    public async Task EnsureCoreAsync_WhenThereIsNoPackage_ShouldStartTheMemoryAsBefore()
    {
        var memory = Service(new InMemoryMemoryStore(), pack: null);

        Assert.Equal(PlatformInstructions.CoreTemplate, await memory.EnsureCoreAsync(_user, None));
        Assert.Equal(StarterSkills.All.Select(s => s.Name).Order(), (await memory.ListSkillsAsync(_user, includePrivate: true, None)).Select(s => s.Skill.Name).Order());
        Assert.Equal(2 + StarterSkills.All.Count, Ok(await memory.ListAsync(_user, "/", includePrivate: true, None)).Count);
        Assert.Equal(string.Empty, PlatformInstructions.PackSection(memory.Pack));
    }

    [Fact]
    public async Task EnsureCoreAsync_WhenThePackageHasACoreAndASkill_ShouldReplaceTheTemplateAndTheStarterOfItsName()
    {
        var folder = Package(new()
        {
            ["memory/filum.md"] = "# About you\n\n# Rules\n- Answer in short sentences.\n\n# How to answer\n\n# Memory map\n",
            ["skills/what-you-know-about-me.md"] = SkillFormat.Format(new Skill("what-you-know-about-me", "A one-line summary", "the person asks what you know", true, "1. Say it in one line.")),
        });
        var memory = Service(new InMemoryMemoryStore(), await PackLoader.LoadAsync(folder, Limits, None));

        Assert.Contains("Answer in short sentences.", await memory.EnsureCoreAsync(_user, None));
        var skills = await memory.ListSkillsAsync(_user, includePrivate: true, None);
        Assert.Equal(StarterSkills.All.Count, skills.Count);
        Assert.Equal("A one-line summary", skills.Single(s => s.Skill.Name == "what-you-know-about-me").Skill.Description);
    }

    [Fact]
    public async Task LoadAsync_WhenThePackageHasProblems_ShouldRefuseItListingEveryProblem()
    {
        var folder = Package(new()
        {
            ["skills/broken.md"] = "no header here\n",
            ["memory/notes.txt"] = "not a memory file\n",
            ["memory/empty.csv"] = string.Empty,
        });

        var refused = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(folder, Limits, None));

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
    public async Task LoadAsync_WhenTheEngineDoesNotUnderstandTheManifest_ShouldRefuseIt(string manifest, string expected)
    {
        var folder = Package([], manifest);

        Assert.Contains((await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(folder, Limits, None))).Problems, p => p.Contains(expected));
    }

    [Fact]
    public Task LoadAsync_WhenTheFolderIsMissing_ShouldRefuseIt() =>
        Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(Path.Combine(_temp, "nowhere"), Limits, None));

    [Fact]
    public async Task TryLoadAsync_WhenThePackageHasProblems_ShouldReturnTheProblemsAndTheMessageLoadAsyncThrows()
    {
        var folder = Package(new() { ["skills/broken.md"] = "no header here\n" });

        var refused = await PackLoader.TryLoadAsync(folder, Limits, None);
        var thrown = await Assert.ThrowsAsync<PackException>(() => PackLoader.LoadAsync(folder, Limits, None));

        Assert.False(refused.IsLoaded);
        Assert.Null(refused.Pack);
        Assert.Equal(thrown.Problems, refused.Problems);
        Assert.Equal(thrown.Message, refused.Message);
    }

    [Fact]
    public async Task TryLoadAsync_WhenTheFolderIsMissing_ShouldRefuseItWithoutThrowing()
    {
        var refused = await PackLoader.TryLoadAsync(Path.Combine(_temp, "nowhere"), Limits, None);

        Assert.False(refused.IsLoaded);
        Assert.Contains("There is no folder at", Assert.Single(refused.Problems));
    }

    [Fact]
    public async Task TryLoadAsync_WhenThePackageIsValid_ShouldReturnItWithNoProblem()
    {
        var loaded = await PackLoader.TryLoadAsync(Path.Combine(Root(), "packs", "example"), Limits, None);

        Assert.True(loaded.IsLoaded);
        Assert.Equal(((await ExampleAsync()).Name, (await ExampleAsync()).Version), (loaded.Pack.Name, loaded.Pack.Version));
        Assert.Empty(loaded.Problems);
        Assert.Null(loaded.Message);
    }

    [Fact]
    public async Task Compose_WhenThereIsAPackage_ShouldPutItsPromptAfterThePlatformLayerAndBeforeTheCoreWhichNeverOverridesIt()
    {
        var text = PlatformInstructions.Compose("CORE-TEXT", "INDEX", "SKILLS", pack: await ExampleAsync());

        var platform = text.IndexOf(PlatformInstructions.Text, StringComparison.Ordinal);
        var prompt = text.IndexOf("Keep the person's journal", StringComparison.Ordinal);
        var core = text.IndexOf("CORE-TEXT", StringComparison.Ordinal);
        Assert.True(platform == 0 && platform < prompt && prompt < core);
        Assert.Contains("the person's core adds to them and never overrides them", text[..core]);
    }

    [Fact]
    public async Task EnsureCoreAsync_WhenTheMemoryWasMadeBeforeThePackage_ShouldKeepItsFilesAndGetThePrompt()
    {
        var store = new InMemoryMemoryStore();
        await Service(store, pack: null).EnsureCoreAsync(_user, None);

        var withPack = Service(store, await ExampleAsync());
        await withPack.EnsureCoreAsync(_user, None);

        Assert.True((await withPack.GetFileAsync(_user, "/journal.csv", None)).IsMissing);
        Assert.Contains("Keep the person's journal", PlatformInstructions.PackSection(withPack.Pack));
    }

    [Fact]
    public async Task MemoryOverview_WhenThereIsAPackage_ShouldStartWithItsRules()
    {
        var memory = Service(new InMemoryMemoryStore(), await ExampleAsync());
        var tools = new MemoryTools(memory, Limits, new JintCodeSandbox(), _user, MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid()));

        var result = await tools.Tools.OfType<AIFunction>().Single(t => t.Name == "memory_overview").InvokeAsync(new AIFunctionArguments());
        var text = result is JsonElement json ? json.GetString()! : (string)result!;

        Assert.StartsWith("# The rules of this assistant (package \"example\")", text);
    }

    private static IMemoryService Service(IMemoryStore store, Pack? pack) =>
        MemoryServices.On(store, Limits, pack: pack);

    private static Task<Pack> ExampleAsync() => PackLoader.LoadAsync(Path.Combine(Root(), "packs", "example"), Limits, None);

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
