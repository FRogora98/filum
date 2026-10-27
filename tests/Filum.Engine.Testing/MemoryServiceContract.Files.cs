using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Abstractions.Options;
using Filum.Engine.Instructions;
using Filum.Engine.Skills;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task WriteAsync_WhenFilesAreWritten_ShouldListReadAndSearchThem()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/plans.md", "# Plans\nVisit the old bridge\r\nCall the library\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/collections/reading.csv", "title,year\nFirst book,2024\nSecond book,2025\n", None));

        var files = Ok(await _memory.ListAsync(_user, "/", false, None));
        Assert.Equal(["/collections/reading.csv", "/notes/plans.md"], files.Select(f => f.Path));
        Assert.Equal(("title,year", 2), (files[0].Header, files[0].RowCount));
        Assert.Equal((3, (string?)null), (files[1].LineCount, files[1].Header));
        Assert.Equal(["/notes/plans.md"], Ok(await _memory.ListAsync(_user, "/notes", false, None)).Select(f => f.Path));

        var read = Ok(await _memory.ReadAsync(_user, "/notes/plans.md", null, null, None));
        Assert.Equal(["# Plans", "Visit the old bridge", "Call the library"], read.Lines);

        var hits = Ok(await _memory.SearchAsync(_user, "BRIDGE", "/", false, None)).Hits;
        Assert.Equal([("/notes/plans.md", 2, "Visit the old bridge")], hits.Select(h => (h.Path, h.Line, h.Text)));
        Assert.Empty(Ok(await _memory.SearchAsync(_user, "50%_off", "/", false, None)).Hits);
    }

    [Fact]
    public async Task EditAsync_WhenTheTextIsFound_ShouldReplaceExactlyOneOccurrence()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "one two\nthree two\n", None));

        Assert.Contains("not found", Refused(await _memory.EditAsync(_user, Agent, "/notes/a.md", "four", "x", None)));
        Assert.Contains("appears 2 times", Refused(await _memory.EditAsync(_user, Agent, "/notes/a.md", "two", "x", None)));
        Ok(await _memory.EditAsync(_user, Agent, "/notes/a.md", "three two", "three 2", None));

        Assert.Equal(["one two", "three 2"], Ok(await _memory.ReadAsync(_user, "/notes/a.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task AppendAsync_WhenTheFileIsMissingOrExists_ShouldCreateItOrAddTheRows()
    {
        var created = Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "item,qty\nfirst,1", None));
        var added = Ok(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "second,2\nthird,3\n", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/log.md", "a line", None));
        Ok(await _memory.AppendAsync(_user, Agent, "/notes/log.md", "another line", None));

        Assert.True(created.Created);
        Assert.Equal((false, 3, 2), (added.Created, added.RowCount, added.Added));
        Assert.Equal(["a line", "another line"], Ok(await _memory.ReadAsync(_user, "/notes/log.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task DeleteAndMoveAsync_WhenAFileIsDeletedOrMoved_ShouldKeepItsHistoryAndProtectTheCore()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/a.md", "a\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/b.md", "b\n", None));

        Assert.Contains("already a file", Refused(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/notes/b.md", None)));
        Assert.Contains("cannot be moved", Refused(await _memory.MoveAsync(_user, Agent, "/filum.md", "/core.md", None)));
        Assert.Contains("cannot be moved", Refused(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/filum.md", None)));
        Assert.Contains("cannot be deleted", Refused(await _memory.DeleteAsync(_user, Agent, "/filum.md", None)));

        var moved = Ok(await _memory.MoveAsync(_user, Agent, "/notes/a.md", "/archive/a.md", None));
        Assert.Equal("/notes/a.md", moved.FromPath);
        Assert.Equal(2, Ok(await _memory.GetHistoryAsync(_user, "/archive/a.md", None)).Count);

        Ok(await _memory.DeleteAsync(_user, Agent, "/notes/b.md", None));
        Assert.Contains("no file", Refused(await _memory.ReadAsync(_user, "/notes/b.md", null, null, None)));
        Assert.Equal(["/archive/a.md", "/filum.md", "/learned.md"], Ok(await _memory.ListAsync(_user, "/", false, None)).Select(f => f.Path).Where(p => !SkillFormat.IsSkillPath(p)));
    }

    [Fact]
    public async Task ListAsync_WhenAFileIsPrivate_ShouldLeaveItOutUnlessAskedFor()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/open.md", "a shared word\n", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/closed.md", "a shared word\n", None));
        Ok(await _memory.SetSensitivityAsync(_user, Agent, "/notes/closed.md", MemorySensitivity.Private, None));

        Assert.Equal(["/notes/open.md"], Ok(await _memory.ListAsync(_user, "/", false, None)).Select(f => f.Path));
        Assert.Equal(["/notes/open.md"], Ok(await _memory.SearchAsync(_user, "shared", "/", false, None)).Hits.Select(h => h.Path));
        Assert.Equal(2, Ok(await _memory.ListAsync(_user, "/", true, None)).Count);
        Assert.Equal(2, Ok(await _memory.SearchAsync(_user, "shared", "/", true, None)).Hits.Count);
        Assert.Equal(MemorySensitivity.Private, Ok(await _memory.ReadAsync(_user, "/notes/closed.md", null, null, None)).Sensitivity);

        Assert.Contains("normal, sensitive or private", Refused(await _memory.SetSensitivityAsync(_user, Agent, "/notes/open.md", "secret", None)));
        await _memory.EnsureCoreAsync(_user, None);
        Assert.Contains("cannot change", Refused(await _memory.SetSensitivityAsync(_user, Agent, "/filum.md", MemorySensitivity.Private, None)));
    }

    [Fact]
    public async Task WriteAsync_WhenACollectionIsLargerThanAFileMayBe_ShouldTakeItUpToTheCollectionLimit()
    {
        var rows = string.Concat(Enumerable.Range(0, 200).Select(i => $"row {i},value {i}\n"));

        Ok(await _memory.WriteAsync(_user, Agent, "/lists/big.csv", "item,value\n" + rows, None));
        var refused = Refused(await _memory.WriteAsync(_user, Agent, "/lists/huge.csv", "item,value\n" + rows + rows + rows, None));

        Assert.Contains("over the limit of 8 KB", refused);
        Assert.Contains("every later change", refused);
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheFactsWouldPassTheCollectionLimit_ShouldSayThatEveryLaterFactWillBeRefused()
    {
        MemoryOutcome<MemoryChange>? last = null;
        for (var i = 0; i < 200 && last is not { Refusal: not null }; i++)
        {
            last = await _memory.RecordFactAsync(_user, Agent, $"subject {i}", "attribute", new string('v', 60), "2026-01-01", null, [], None);
        }

        var refused = Refused(last!);
        Assert.Contains("/facts.csv", refused);
        Assert.Contains("every later change", refused);
        Assert.Contains("tell the person", refused);
    }

    [Fact]
    public async Task WriteAsync_WhenOverALimit_ShouldRefuseAndLeaveTheMemoryAsItWas()
    {
        await _memory.EnsureCoreAsync(_user, None);
        Assert.Contains("over the limit of 2 KB", Refused(await _memory.WriteAsync(_user, Agent, "/notes/big.md", new string('x', 2100), None)));
        Assert.Contains("condense it", Refused(await _memory.WriteAsync(_user, Agent, "/filum.md", new string('x', 301), None)));
        Assert.Equal(PlatformInstructions.CoreTemplate, await _memory.EnsureCoreAsync(_user, None));

        Ok(await _memory.WriteAsync(_user, Agent, "/notes/1.md", "1", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/2.md", "2", None));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/3.md", "3", None));
        Assert.Contains("memory is full (8 files)", Refused(await _memory.WriteAsync(_user, Agent, "/notes/4.md", "4", None)));
        Ok(await _memory.WriteAsync(_user, Agent, "/notes/3.md", "replacing is fine", None));

        Assert.Contains("'..'", Refused(await _memory.WriteAsync(_user, Agent, "/notes/../x.md", "x", None)));
        Assert.Contains("Only .md", Refused(await _memory.WriteAsync(_user, Agent, "/notes/x.txt", "x", None)));
        Assert.Equal(8, Ok(await _memory.ListAsync(_user, "/", true, None)).Count);
    }

    [Fact]
    public async Task AppendAsync_WhenACollectionRowHasTheWrongNumberOfFields_ShouldRefuse()
    {
        Ok(await _memory.WriteAsync(_user, Agent, "/collections/list.csv", "a,b,c\n1,2,3\n", None));

        Assert.Contains("Line 3 has 2 fields, the header has 3", Refused(await _memory.AppendAsync(_user, Agent, "/collections/list.csv", "4,5", None)));
        Assert.Contains("Line 3 has 4 fields", Refused(await _memory.WriteAsync(_user, Agent, "/collections/list.csv", "a,b,c\n1,2,3\n1,2,3,4\n", None)));

        Assert.Equal(["a,b,c", "1,2,3"], Ok(await _memory.ReadAsync(_user, "/collections/list.csv", null, null, None)).Lines);
    }

    [Fact]
    public async Task ReadAsync_WhenTheFileIsLong_ShouldReadItInPages()
    {
        var memory = Memory(new MemoryOptions());
        Ok(await memory.WriteAsync(_user, Agent, "/notes/long.md", string.Join("\n", Enumerable.Range(1, 1200).Select(i => $"line {i}")), None));

        var first = Ok(await memory.ReadAsync(_user, "/notes/long.md", null, null, None));
        var rest = Ok(await memory.ReadAsync(_user, "/notes/long.md", 1001, 2000, None));

        Assert.Equal((1, 500, 1200, 500), (first.FromLine, first.ToLine, first.TotalLines, first.Lines.Count));
        Assert.Equal(("line 1001", "line 1200", 200), (rest.Lines[0], rest.Lines[^1], rest.Lines.Count));
        Assert.Contains("has 1200 lines", Refused(await memory.ReadAsync(_user, "/notes/long.md", 1300, null, None)));
    }

    [Fact]
    public async Task MemoryService_WhenAnotherPersonAsks_ShouldShowNoMemoryOfTheFirst()
    {
        var other = Guid.NewGuid();
        var change = Ok(await _memory.WriteAsync(_user, Agent, "/notes/mine.md", "mine\n", None));

        Assert.Empty(Ok(await _memory.ListAsync(other, "/", true, None)));
        Assert.Empty(Ok(await _memory.SearchAsync(other, "mine", "/", true, None)).Hits);
        Assert.Contains("no file", Refused(await _memory.ReadAsync(other, "/notes/mine.md", null, null, None)));
        Assert.Contains("no file", Refused(await _memory.EditAsync(other, Agent, "/notes/mine.md", "mine", "yours", None)));
        Assert.Contains("no file", Refused(await _memory.DeleteAsync(other, Agent, "/notes/mine.md", None)));
        Assert.Contains("no file", Refused(await _memory.GetHistoryAsync(other, "/notes/mine.md", None)));
        Assert.Contains("no change", Refused(await _memory.UndoAsync(other, MemoryActor.Person, change.RevisionId, None)));

        if (OnePersonPerStore)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _memory.WriteAsync(other, Agent, "/notes/mine.md", "theirs\n", None));
        }
        else
        {
            Ok(await _memory.WriteAsync(other, Agent, "/notes/mine.md", "theirs\n", None));
        }

        Assert.Equal(["mine"], Ok(await _memory.ReadAsync(_user, "/notes/mine.md", null, null, None)).Lines);
    }

    [Fact]
    public async Task MemoryService_WhenTheFileIsMissing_ShouldSayMissingRatherThanRefused()
    {
        Assert.True((await _memory.ReadAsync(_user, "/notes/none.md", null, null, None)).IsMissing);
        Assert.True((await _memory.EditAsync(_user, Agent, "/notes/none.md", "a", "b", None)).IsMissing);
        Assert.True((await _memory.DeleteAsync(_user, Agent, "/notes/none.md", None)).IsMissing);
        Assert.True((await _memory.GetHistoryAsync(_user, "/notes/none.md", None)).IsMissing);
        Assert.True((await _memory.UndoAsync(_user, Agent, long.MaxValue, None)).IsMissing);

        var refused = await _memory.WriteAsync(_user, Agent, "/notes/x.txt", "x", None);
        Assert.True(refused.IsRefused);
        Assert.False(refused.IsMissing);
    }
}
