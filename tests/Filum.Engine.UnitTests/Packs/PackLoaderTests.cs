using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Packs;
using Filum.Engine.UnitTests.Fakes;

namespace Filum.Engine.UnitTests.Packs;

public sealed class PackLoaderTests
{
    private const string Manifest = """{"format": 1, "name": "notes", "version": "1.0.0", "description": "d", "private": ["/diary.md"]}""";

    [Fact]
    public async Task TryLoadFromAsync_WhenTheSourceHoldsAValidPackage_ShouldLoadItWithItsFilesAndTheirSensitivity()
    {
        var source = new InMemoryPackSource(new PackContent(Manifest, "Answer briefly.\r\n", [], [("/diary.md", "# Diary\r\n"), ("/notes.md", "# Notes\n")]));

        var loaded = await PackLoader.TryLoadFromAsync(source, "anywhere", new MemoryOptions(), CancellationToken.None);

        Assert.True(loaded.IsLoaded, loaded.Message);
        Assert.Equal(("notes", "1.0.0", "Answer briefly."), (loaded.Pack.Name, loaded.Pack.Version, loaded.Pack.Prompt));
        Assert.Equal([("/diary.md", "# Diary\n", MemorySensitivity.Private), ("/notes.md", "# Notes\n", MemorySensitivity.Normal)],
            loaded.Pack.Files.Select(f => (f.Path, f.Content, f.Sensitivity)));
    }

    [Fact]
    public async Task TryLoadFromAsync_WhenTheSourceHasNoPackage_ShouldRefuseItWithoutThrowing()
    {
        var refused = await PackLoader.TryLoadFromAsync(new InMemoryPackSource(null), "nowhere", new MemoryOptions(), CancellationToken.None);

        Assert.False(refused.IsLoaded);
        Assert.StartsWith("The package in nowhere cannot be used:", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryLoadFromAsync_WhenTheManifestIsMissingAndASkillIsNotMarkdown_ShouldListBothProblems()
    {
        var source = new InMemoryPackSource(new PackContent(null, null, [("review.txt", "steps")], []));

        var refused = await PackLoader.TryLoadFromAsync(source, "pack", new MemoryOptions(), CancellationToken.None);

        Assert.Contains("pack.json is missing.", refused.Problems);
        Assert.Contains("skills/review.txt: a skill file ends in .md.", refused.Problems);
    }
}
