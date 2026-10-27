namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class FolderLayoutTests
{
    private static readonly FolderLayout Layout = new(Path.Combine(Path.GetTempPath(), "memory"));

    [Fact]
    public void Full_WhenGivenAMemoryPath_ShouldBeThatFileUnderTheRoot()
    {
        Assert.Equal(Path.Combine(Layout.Root, "notes", "plans.md"), Layout.Full("/notes/plans.md"));
    }

    [Fact]
    public void Files_WhenTheLayoutIsMade_ShouldKeepTheFoldersOwnFilesInItsMetaFolder()
    {
        // The names are the on-disk format: a folder written by any version since 0.7 has them.
        Assert.Equal(Path.Combine(Layout.Root, ".filum"), Layout.Meta);
        Assert.Equal(
            ["owner", "lock", "state.json", "revisions.jsonl", "events.jsonl", "vectors.jsonl"],
            new[] { Layout.OwnerFile, Layout.LockFile, Layout.StateFile, Layout.RevisionsFile, Layout.EventsFile, Layout.VectorsFile }.Select(Path.GetFileName));
        Assert.All(new[] { Layout.OwnerFile, Layout.StateFile, Layout.EventsFile }, f => Assert.Equal(Layout.Meta, Path.GetDirectoryName(f)));
        Assert.Equal(Layout.Temporary, Path.GetDirectoryName(Layout.NewTemporaryFile()));
    }
}
