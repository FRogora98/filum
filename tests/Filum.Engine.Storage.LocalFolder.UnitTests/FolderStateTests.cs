using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class FolderStateTests
{
    private static readonly FolderState State = new()
    {
        Files =
        [
            new FileEntry { Path = "/notes/open.md" },
            new FileEntry { Path = "/notes/closed.md", Sensitivity = MemorySensitivity.Private },
            new FileEntry { Path = "/lists/a.csv" },
            new FileEntry { Path = "/notes/gone.md", DeletedAt = DateTimeOffset.UnixEpoch },
        ],
    };

    [Fact]
    public void Visible_WhenAskedUnderAPrefix_ShouldGiveTheLiveFilesThereAndPrivateOnesOnlyWhenAsked()
    {
        Assert.Equal(["/notes/open.md"], State.Visible("/notes", includePrivate: false).Select(f => f.Path));
        Assert.Equal(["/notes/open.md", "/notes/closed.md"], State.Visible("/notes", includePrivate: true).Select(f => f.Path));
        Assert.Equal(["/notes/open.md", "/lists/a.csv"], State.Visible("/", includePrivate: false).Select(f => f.Path));
        Assert.Equal(3, State.Visible(null, includePrivate: true).Count());
    }
}
