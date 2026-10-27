using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Facts;

namespace Filum.Engine.Testing;

// Spec 041: the facts as a ledger.
public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task RecordFactAsync_WhenTheTableHasTheSevenColumnsOfBefore_ShouldReadItAndAddTheNewOnesOnTheNextWrite()
    {
        Ok(await _memory.WriteAsync(_user, Agent, FactTable.Path,
            "subject,attribute,value,valid_from,valid_to,sources,note\nSam,city,Turin,2026-01-01,,,\n", None));

        Assert.Equal(["Turin"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "job", "baker", "2026-02-01", null, [], None));

        var lines = Ok(await _memory.ReadAsync(_user, FactTable.Path, null, null, None)).Lines;
        Assert.Equal(string.Join(",", FactTable.Header), lines[0]);
        Assert.Equal(11, FactTable.Header.Count);
        Assert.StartsWith("Sam,city,Turin,2026-01-01,,,,", lines[1]);
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheValueWasWrong_ShouldMarkItCorrectedSoItNeverHeld()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Turin", "2026-01-01", null, [], None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Lyon", null, null, [], correction: true, None));

        var history = Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None));
        Assert.Equal([("Turin", "2026-01-01", "2026-01-01", FactEnding.Corrected), ("Lyon", "2026-01-01", "", "")], history.Select(f => (f.Value, f.ValidFrom, f.ValidTo, f.Ended)));
        Assert.Equal(["Lyon"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
        Assert.Equal(["Lyon"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", "2026-02-01", None)).Select(f => f.Value));
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheValueChanged_ShouldKeepTheOldOneAsWhatHeldBefore()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Turin", "2026-01-01", null, [], None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Lyon", "2026-03-01", null, [], None));

        Assert.Equal(FactEnding.Changed, Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None))[0].Ended);
        Assert.Equal(["Turin"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", "2026-02-01", None)).Select(f => f.Value));
        Assert.Equal(["Lyon"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", "2026-03-01", None)).Select(f => f.Value));
    }

    [Fact]
    public async Task EndFactAsync_WhenAFactNoLongerHolds_ShouldEndItWithoutANewValue()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "car", "a blue hatchback", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "bike", "a road bike", "2026-01-01", null, [], None));

        Ok(await _memory.EndFactAsync(_user, Agent, "the person", "car", "2026-04-01", retract: false, value: null, None));
        Ok(await _memory.EndFactAsync(_user, Agent, "the person", "bike", null, retract: true, value: null, None));

        Assert.Empty(Ok(await _memory.CurrentFactsAsync(_user, "the person", None)));
        var car = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "the person", "car", None)));
        Assert.Equal(("2026-04-01", FactEnding.Ended), (car.ValidTo, car.Ended));
        var bike = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "the person", "bike", None)));
        Assert.Equal(("2026-01-01", FactEnding.Retracted), (bike.ValidTo, bike.Ended));
        Assert.DoesNotContain(Ok(await _memory.CurrentFactsAsync(_user, "the person", "2026-02-01", None)), f => f.Attribute == "bike");
        Assert.Contains("no open", Refused(await _memory.EndFactAsync(_user, Agent, "the person", "boat", null, retract: false, value: null, None)));
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheOpenValueChangesTheSameDay_ShouldCloseItThatDayAsChanged()
    {
        // A value can change twice in a day ("the meeting moved to Tuesday, then to Friday"); facts given in order with no
        // day of their own are all of today. The later wins; only a day already closed in the past is ambiguous.
        Ok(await _memory.RecordFactAsync(_user, Agent, "the meeting", "day", "Tuesday", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the meeting", "day", "Friday", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the meeting", "room", "A", null, null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the meeting", "room", "B", null, null, [], None));

        Assert.Equal(["B", "Friday"], Ok(await _memory.CurrentFactsAsync(_user, "the meeting", None)).Select(f => f.Value).Order());
        var tuesday = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "the meeting", "day", None)), f => f.Value == "Tuesday");
        Assert.Equal(("2026-01-01", FactEnding.Changed), (tuesday.ValidTo, tuesday.Ended));
    }

    [Fact]
    public async Task CurrentFactsAsync_WhenTheSubjectIsNamedByAnAlias_ShouldGiveTheFactsOfTheSubject()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam Lee", FactTable.Alias, "Sam", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam Lee", "city", "Lyon", "2026-01-01", null, [], None));

        Assert.Contains("Lyon", Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
        Assert.Contains("Lyon", Ok(await _memory.FactHistoryAsync(_user, "sam", "city", None)).Select(f => f.Value));
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheAgentRecordsAFactFromAnEvent_ShouldKeepWhenWhoAndTheWords()
    {
        var said = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "My sister Ada moved to Porto last spring."), None);

        Ok(await _memory.RecordFactAsync(_user, Agent, "Ada", "city", "Porto", null, null, [said.Id], None));

        var fact = Assert.Single(Ok(await _memory.CurrentFactsAsync(_user, "Ada", None)));
        Assert.Equal(MemoryAuthor.Agent, fact.Author);
        Assert.Equal("My sister Ada moved to Porto last spring.", fact.Quote);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", fact.RecordedAt);
    }
}
