
namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

public sealed class JsonLinesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-jsonlines-" + Guid.NewGuid().ToString("N"));
    private readonly FolderLayout _layout;

    public JsonLinesTests()
    {
        _layout = new FolderLayout(_folder);
        Directory.CreateDirectory(_layout.Temporary);
    }

    private string Log => _layout.EventsFile;

    public void Dispose() => Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);

    [Fact]
    public void Append_WhenTheLastLineWasCutShort_ShouldCloseItFirst()
    {
        File.WriteAllText(Log, "{\"id\":1}\n{\"id\":");

        JsonLines.Append(Log, "{\"id\":2}");

        Assert.Equal("{\"id\":1}\n{\"id\":\n{\"id\":2}\n", File.ReadAllText(Log));
    }

    [Fact]
    public void Read_WhenALineDoesNotParse_ShouldSkipItAndKeepTheOthers()
    {
        File.WriteAllText(Log, "{\"id\":1}\nnot json\n\n{\"id\":2}\n{\"id\":");

        var ids = JsonLines.Read<Line>(Log).Select(l => l.Id);

        Assert.Equal([1L, 2L], ids);
    }

    [Fact]
    public void Rewrite_WhenALineDoesNotParse_ShouldKeepItInTheUnreadableFile()
    {
        File.WriteAllText(Log, "{\"id\":1}\nnot json\n{\"id\":2}\n");

        JsonLines.Rewrite<Line>(_layout, Log, ["{\"id\":2}"]);

        Assert.Equal("{\"id\":2}\n", File.ReadAllText(Log));
        Assert.Equal("not json\n", File.ReadAllText(Log + ".unreadable"));
    }

    private sealed record Line(long Id);
}
