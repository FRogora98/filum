namespace Filum.Engine.Storage.LocalFolder.IntegrationTests;

// The format a folder was written in is kept in .filum/format, so an engine never reads a newer one as empty.
public sealed class FolderFormatTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "filum-tests", Guid.NewGuid().ToString("N"));

    private string FormatFile => Path.Combine(_folder, ".filum", "format");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Filum.Engine.Testing.Builders.TemporaryFolders.Delete(_folder);
        }
    }

    [Fact]
    public void Open_WhenTheFolderIsNew_ShouldWriteTheFormatItIsWrittenIn()
    {
        using var store = new LocalFolderStore(_folder);

        Assert.Equal("3", File.ReadAllText(FormatFile).Trim());
    }

    [Fact]
    public void Open_WhenTheFolderHasNoFormatYet_ShouldTakeItAsTheFirstAndMoveItOn()
    {
        Directory.CreateDirectory(Path.Combine(_folder, ".filum"));
        File.WriteAllText(Path.Combine(_folder, ".filum", "owner"), Guid.NewGuid().ToString());

        using var store = new LocalFolderStore(_folder);

        Assert.Equal("3", File.ReadAllText(FormatFile).Trim());
    }

    [Fact]
    public void Open_WhenANewerFilumWroteTheFolder_ShouldRefuseItAndSayWhy()
    {
        Directory.CreateDirectory(Path.Combine(_folder, ".filum"));
        File.WriteAllText(FormatFile, "4\n");

        var error = Assert.Throws<InvalidOperationException>(() => new LocalFolderStore(_folder));

        Assert.Contains("format 4", error.Message, StringComparison.Ordinal);
        Assert.Contains("newer version of Filum", error.Message, StringComparison.Ordinal);
        Assert.Equal("4", File.ReadAllText(FormatFile).Trim());
    }

    [Fact]
    public void Open_WhenTheFolderIsInFormat2_ShouldMoveItOnTo3()
    {
        Directory.CreateDirectory(Path.Combine(_folder, ".filum"));
        File.WriteAllText(Path.Combine(_folder, ".filum", "owner"), Guid.NewGuid().ToString());
        File.WriteAllText(FormatFile, "2\n");

        using var store = new LocalFolderStore(_folder);

        Assert.Equal("3", File.ReadAllText(FormatFile).Trim());
    }
}
