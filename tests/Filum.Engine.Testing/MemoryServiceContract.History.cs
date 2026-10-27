using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task GetHistoryAsync_WhenFilesChange_ShouldGiveARevisionPerChangeWithItsAuthor()
    {
        var turn = MemoryActor.Agent(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        Ok(await _memory.WriteAsync(_user, turn, "/notes/a.md", "first\n", None));
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "second", None));

        var history = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None));

        Assert.Equal([(MemoryOperation.Append, MemoryAuthor.Person), (MemoryOperation.Write, MemoryAuthor.Agent)], history.Select(r => (r.Operation, r.Author)));
        Assert.Equal((turn.ConversationId, turn.MessageId), (history[1].ConversationId, history[1].MessageId));
        Assert.All(history, r => Assert.InRange(r.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5)));
        Assert.Equal("first\n", Ok(await _memory.GetRevisionAsync(_user, history[1].Id, None)).Content);
    }

    [Fact]
    public async Task AppendAsync_WhenTwoChangesStartFromTheSameVersion_ShouldLetOnlyOneWin()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "start\n", None));
        var racing = RacingService(2);

        var results = await Task.WhenAll(
            racing.AppendAsync(_user, Agent, "/notes/a.md", "from one", None),
            racing.AppendAsync(_user, Agent, "/notes/a.md", "from two", None));

        Assert.Single(results, r => !r.IsRefused);
        Assert.Contains("changed at the same time", Assert.Single(results, r => r.IsRefused).Refusal);
        Assert.Equal(2, Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).TotalLines);
    }

    [Fact]
    public async Task WriteAsync_WhenTwoCreationsOfTheSamePathRace_ShouldLetOnlyOneWin()
    {
        var racing = RacingService(2);

        var results = await Task.WhenAll(
            racing.WriteAsync(_user, Agent, "/notes/new.md", "one\n", None),
            racing.WriteAsync(_user, Agent, "/notes/new.md", "two\n", None));

        Assert.Single(results, r => !r.IsRefused);
        Assert.Contains("changed at the same time", Assert.Single(results, r => r.IsRefused).Refusal);
        Assert.Single(Ok(await _memory.ListAsync(_user, "/", true, None)));
    }

    [Fact]
    public async Task UndoAsync_WhenTheChangeIsTheLatest_ShouldBringBackThePreviousStateAsANewRevision()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        var edit = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));

        var undo = Ok(await _memory.UndoAsync(_user, MemoryActor.Person, edit.RevisionId, None));

        Assert.Equal(["v1"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        var history = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None));
        Assert.Equal((undo.RevisionId, MemoryOperation.Undo, MemoryAuthor.Person, (long?)edit.RevisionId), (history[0].Id, history[0].Operation, history[0].Author, history[0].UndoesRevisionId));
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public async Task UndoAsync_WhenTheChangeIsACreationADeleteOrAMove_ShouldTakeItBack()
    {
        var created = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, created.RevisionId, None));
        Assert.Empty(Ok(await _memory.ListAsync(_user, "/", true, None)));

        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));
        var deleted = Ok(await _memory.DeleteAsync(_user, Agent, "/notes/b.md", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, deleted.RevisionId, None));
        Assert.Equal(["b"], Ok(await _memory.ReadAsync(_user, "/notes/b.md", null, null, None)).Lines);

        var moved = Ok(await _memory.MoveAsync(_user, Agent, "/notes/b.md", "/archive/b.md", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, moved.RevisionId, None));
        Assert.Equal(["/notes/b.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
    }

    [Fact]
    public async Task UndoAsync_WhenTheFileChangedAgainAfterwards_ShouldRefuse()
    {
        var first = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        var later = Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "v2", None));

        var refusal = Refused(await _memory.UndoAsync(_user, MemoryActor.Person, first.RevisionId, None));

        Assert.Contains($"change {later.RevisionId}", refusal);
        Assert.Contains("append by you", refusal);
        Assert.Equal(["v1", "v2"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task RollbackAsync_WhenATurnMadeChanges_ShouldTakeThemBackNewestFirst()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/kept.md", "before\n", None));
        var turn = new List<long>
        {
            Ok(await _memory.AppendAsync(_user, Agent, "/notes/kept.md", "during", None)).RevisionId,
            Ok(await _memory.WriteAsync(_user, Agent, "/notes/new.md", "new\n", None)).RevisionId,
            Ok(await _memory.MoveAsync(_user, Agent, "/notes/kept.md", "/notes/moved.md", None)).RevisionId
        };

        await _memory.RollbackAsync(_user, turn, None);

        Assert.Equal(["/notes/kept.md"], Ok(await _memory.ListAsync(_user, "/", true, None)).Select(f => f.Path));
        Assert.Equal(["before"], Ok(await _memory.ReadAsync(_user, "/notes/kept.md", null, null, None)).Lines);
        Assert.Equal(MemoryOperation.Rollback, Ok(await _memory.GetHistoryAsync(_user, "/notes/kept.md", None))[0].Operation);
    }

    [Fact]
    public async Task RollbackAsync_WhenSomeoneElseChangedTheFileSince_ShouldKeepIt()
    {
        var turn = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "from the turn\n", None)).RevisionId;
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/notes/a.md", "from the person", None));

        await _memory.RollbackAsync(_user, [turn], None);

        Assert.Equal(["from the turn", "from the person"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task RestoreAsync_WhenLaterChangesExist_ShouldBringBackTheChosenVersion()
    {
        var first = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/a.md", "v3", None));

        var restored = Ok(await _memory.RestoreAsync(_user, MemoryActor.Person, first.RevisionId, None));

        Assert.Equal(["v1"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
        var newest = Ok(await _memory.GetHistoryAsync(_user, "/notes/a.md", None))[0];
        Assert.Equal((restored.RevisionId, MemoryOperation.Restore, MemoryAuthor.Person, "Restored an earlier version"), (newest.Id, newest.Operation, newest.Author, newest.Summary));
    }

    [Fact]
    public async Task RestoreAsync_WhenTheVersionIsADeletionOrItsPathIsTaken_ShouldRefuse()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        var deleted = Ok(await _memory.DeleteAsync(_user, Agent, "/notes/a.md", None));
        Assert.Contains("deletion of the file", Refused(await _memory.RestoreAsync(_user, MemoryActor.Person, deleted.RevisionId, None)));

        var old = Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));
        Ok(await _memory.MoveAsync(_user, Agent, "/notes/b.md", "/notes/c.md", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "another b\n", None));
        Assert.Contains("Another file now uses /notes/b.md", Refused(await _memory.RestoreAsync(_user, MemoryActor.Person, old.RevisionId, None)));

        var missing = await _memory.RestoreAsync(Guid.NewGuid(), MemoryActor.Person, old.RevisionId, None);
        Assert.True(missing.IsMissing);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenAFileChangesInEveryWay_ShouldSayWhatEachChangeDid()
    {
        Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "item\nfirst\nsecond", None));
        Ok(await _memory.AppendAsync(_user, MemoryActor.Person, "/collections/list.csv", "third", None));
        Ok(await _memory.EditAsync(_user, Agent, "/collections/list.csv", "third", "3rd", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/collections/list.csv", MemorySensitivity.Private, None));
        Ok(await _memory.MoveAsync(_user, Agent, "/collections/list.csv", "/collections/items.csv", None));
        var write = Ok(await _memory.WriteAsync(_user, Agent, "/collections/items.csv", "item\nonly\n", None));
        Ok(await _memory.UndoAsync(_user, MemoryActor.Person, write.RevisionId, None));

        var history = Ok(await _memory.GetHistoryAsync(_user, "/collections/items.csv", None));

        Assert.Equal(
            ["Undid a change", "Rewrote", "Moved from /collections/list.csv", "Marked private", "Changed rows", "Added 1 row", "Created with 2 rows"],
            history.Select(r => r.Summary));
    }

    [Fact]
    public async Task GetRevisionAsync_WhenAskedById_ShouldGiveTheVersionOnlyToItsOwner()
    {
        var change = Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v1\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "v2\n", None));

        var version = Ok(await _memory.GetRevisionAsync(_user, change.RevisionId, None));

        Assert.Equal(("/notes/a.md", "v1\n", false), (version.Path, version.Content, version.Deleted));
        Assert.True((await _memory.GetRevisionAsync(Guid.NewGuid(), change.RevisionId, None)).IsMissing);
    }
}
