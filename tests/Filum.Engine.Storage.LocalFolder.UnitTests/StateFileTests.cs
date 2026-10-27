using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Storage;

namespace Filum.Engine.Storage.LocalFolder.UnitTests;

public sealed class StateFileTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid List = Guid.NewGuid();
    private static readonly Guid Note = Guid.NewGuid();

    [Fact]
    public void Rebuild_WhenGivenTheRevisions_ShouldGiveEachFileItsLatestStateAndCountItsVersions()
    {
        var state = StateFile.Rebuild(
        [
            Revision(1, List, "/lists/a.csv", "item\nfirst\n", Start),
            Revision(2, Note, "/notes/n.md", "one\ntwo\n", Start.AddHours(1)),
            Revision(3, List, "/lists/b.csv", "item\nfirst\nsecond\n", Start.AddHours(2)),
        ]);

        Assert.Equal(3, state.LastRevision);
        var list = state.Files.Single(f => f.Id == List);
        Assert.Equal(("/lists/b.csv", 2, 3L, "item", 2), (list.Path, list.Version, list.LatestRevisionId, list.Header, list.RowCount));
        Assert.Equal((Start, Start.AddHours(2), (DateTimeOffset?)null), (list.CreatedAt, list.UpdatedAt, list.DeletedAt));
        var note = state.Files.Single(f => f.Id == Note);
        Assert.Equal((2, (string?)null, (int?)null), (note.LineCount, note.Header, note.RowCount));
        Assert.Equal((0L, default(DateTime)), (note.DiskLength, note.DiskTimeUtc));
    }

    [Fact]
    public void Rebuild_WhenTheLatestRevisionDeletesTheFile_ShouldKeepItAsDeleted()
    {
        var state = StateFile.Rebuild(
        [
            Revision(1, Note, "/notes/n.md", "one\n", Start),
            Revision(2, Note, "/notes/n.md", "one\n", Start.AddHours(1), deleted: true),
        ]);

        Assert.Equal(Start.AddHours(1), Assert.Single(state.Files).DeletedAt);
        Assert.Empty(state.Live());
    }

    private static StoredRevision Revision(long id, Guid file, string path, string content, DateTimeOffset at, bool deleted = false) =>
        new(id, file, path, content, MemorySensitivity.Normal, deleted, deleted ? MemoryOperation.Delete : MemoryOperation.Write, MemoryAuthor.Agent, null, null, null, at);
}
