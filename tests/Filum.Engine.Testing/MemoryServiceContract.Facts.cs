using Filum.Engine.Facts;

namespace Filum.Engine.Testing;

public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task RecordFactAsync_WhenTheValueChanges_ShouldCloseTheOldValueAndKeepBothWithTheirSources()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "3", "2026-01-10", null, [4], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "lives in", "the coast", "2026-01-10", null, [4], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "The Person", "Plants", "4", "2026-02-01", "a new fern", [9, 8], None));

        var now = Ok(await _memory.CurrentFactsAsync(_user, "the person", None));
        Assert.Equal([("lives in", "the coast"), ("Plants", "4")], now.Select(f => (f.Attribute, f.Value)).OrderBy(f => f.Attribute, StringComparer.OrdinalIgnoreCase));
        var history = Ok(await _memory.FactHistoryAsync(_user, "the person", "plants", None));
        Assert.Equal([("3", "2026-01-10", "2026-02-01"), ("4", "2026-02-01", "")], history.Select(f => (f.Value, f.ValidFrom, f.ValidTo)));
        Assert.Equal([4L], history[0].Sources);
        Assert.Equal([8L, 9L], history[1].Sources);
        Assert.Equal("a new fern", history[1].Note);

        var file = Ok(await _memory.ReadAsync(_user, FactTable.Path, null, null, None));
        Assert.Equal(string.Join(",", FactTable.Header), file.Lines[0]);
        // Spec 041: the same value again succeeds and writes nothing; an earlier value is placed in time.
        var revisions = Ok(await _memory.GetHistoryAsync(_user, FactTable.Path, None)).Count;
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "4", null, null, [], None));
        Assert.Equal(revisions, Ok(await _memory.GetHistoryAsync(_user, FactTable.Path, None)).Count);
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "5", "2026-01-15", null, [], None));
        Assert.Equal([("3", "2026-01-10", "2026-01-15"), ("5", "2026-01-15", "2026-02-01"), ("4", "2026-02-01", "")],
            Ok(await _memory.FactHistoryAsync(_user, "the person", "plants", None)).Select(f => (f.Value, f.ValidFrom, f.ValidTo)));
        Assert.Contains("yyyy-MM-dd", Refused(await _memory.RecordFactAsync(_user, Agent, "the person", "plants", "5", "next week", null, [], None)));
        Assert.Empty(Ok(await _memory.CurrentFactsAsync(_user, "someone else", None)));
    }
}
