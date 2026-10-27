using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Facts;
using Filum.Engine.Facts;

namespace Filum.Engine.Testing;

// Spec 041, amended after the independent audit: values placed on days, corrections of a day, several aliases, quotes
// that respect privacy.
public abstract partial class MemoryServiceContract
{
    [Fact]
    public async Task RecordFactAsync_WhenThreeValuesComeTheSameDay_ShouldKeepTheLastAsCurrentAndTheOthersAsChanged()
    {
        foreach (var value in new[] { "A", "B", "C" })
        {
            Ok(await _memory.RecordFactAsync(_user, Agent, "the room", "colour", value, null, null, [], None));
            Ok(await _memory.RecordFactAsync(_user, Agent, "the desk", "colour", value, "2026-01-01", null, [], None));
        }

        Assert.Equal(["C", "C"], Ok(await _memory.CurrentFactsAsync(_user, null, None)).Select(f => f.Value));
        Assert.All(Ok(await _memory.FactHistoryAsync(_user, "the desk", "colour", None)).Where(f => f.Value != "C"),
            f => Assert.Equal(("2026-01-01", FactEnding.Changed), (f.ValidTo, f.Ended)));
    }

    [Fact]
    public async Task RecordFactAsync_WhenAnEarlierDayAlreadyHasAValue_ShouldPutTheNewOneInItsPlaceUntilTheNextValue()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Turin", "2026-03-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-01-01", null, [], None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Rome", "2026-01-01", null, [], None));

        Assert.Equal(["Turin"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
        Assert.Equal(["Rome"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", "2026-02-01", None)).Select(f => f.Value));
        var paris = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None)), f => f.Value == "Paris");
        Assert.Equal(("2026-01-01", FactEnding.Changed), (paris.ValidTo, paris.Ended));
    }

    [Fact]
    public async Task RecordFactAsync_WhenACorrectionNamesADay_ShouldCorrectTheValueOfThatDayOnly()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Turin", "2026-03-01", null, [], None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Rome", "2026-01-01", null, [], correction: true, None));

        var history = Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None));
        Assert.Equal(FactEnding.Corrected, Assert.Single(history, f => f.Value == "Paris").Ended);
        Assert.Equal(("2026-03-01", FactEnding.Changed), Assert.Single(history, f => f.Value == "Rome") is var rome ? (rome.ValidTo, rome.Ended) : default);
        Assert.Equal(["Turin"], Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
    }

    [Fact]
    public async Task RecordFactAsync_WhenASubjectHasSeveralAliases_ShouldAnswerToEachOfThem()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam Lee", FactTable.Alias, "Sam", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam Lee", FactTable.Alias, "Sammy", "2026-01-02", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam Lee", "city", "Lyon", "2026-01-01", null, [], None));

        Assert.Contains("Lyon", Ok(await _memory.CurrentFactsAsync(_user, "Sam", None)).Select(f => f.Value));
        Assert.Contains("Lyon", Ok(await _memory.CurrentFactsAsync(_user, "Sammy", None)).Select(f => f.Value));
    }

    [Fact]
    public async Task RecordFactAsync_WhenAnEarlierValueComesAfterTheFactEnded_ShouldCloseItWhereTheNextValueStarts()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "car", "a van", "2026-01-01", null, [], None));
        Ok(await _memory.EndFactAsync(_user, Agent, "the person", "car", "2026-04-01", retract: false, value: null, None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "car", "a bike", "2025-06-01", null, [], None));

        var bike = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "the person", "car", None)), f => f.Value == "a bike");
        Assert.Equal(("2026-01-01", FactEnding.Changed), (bike.ValidTo, bike.Ended));
        Assert.Empty(Ok(await _memory.CurrentFactsAsync(_user, "the person", None)));
    }

    [Fact]
    public async Task RecordFactAsync_WhenAClosedValueIsSaidAgainForADayItHeld_ShouldChangeNothing()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-01-01", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Turin", "2026-03-01", null, [], None));

        var again = Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-02-01", null, [], None));

        Assert.Equal(MemoryOperation.Unchanged, again.Operation);
        Assert.Equal(2, Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None)).Count);
    }

    [Fact]
    public async Task RecordFactAsync_WhenAValueIsPlacedInsideAnEndedTime_ShouldEndWhereThatTimeEnded()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "car", "a van", "2026-01-01", null, [], None));
        Ok(await _memory.EndFactAsync(_user, Agent, "the person", "car", "2026-04-01", retract: false, value: null, None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", "car", "a bike", "2026-02-01", null, [], None));

        var bike = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "the person", "car", None)), f => f.Value == "a bike");
        Assert.Equal(("2026-04-01", FactEnding.Ended), (bike.ValidTo, bike.Ended));
        Assert.Empty(Ok(await _memory.CurrentFactsAsync(_user, "the person", None)));
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheNextValueIsSaidFromAnEarlierDay_ShouldStartItThenWithoutADuplicate()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-03-01", null, [], None));

        Ok(await _memory.RecordFactAsync(_user, Agent, "Sam", "city", "Paris", "2026-01-01", null, [], None));

        var paris = Assert.Single(Ok(await _memory.FactHistoryAsync(_user, "Sam", "city", None)));
        Assert.Equal("2026-01-01", paris.ValidFrom);
        Assert.True(paris.IsOpen);
    }

    [Fact]
    public async Task RecordFactAsync_WhenRulesAndOpenItemsAreRecorded_ShouldHoldManyAndEndOneByItsValue()
    {
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", FactTable.Rule, "answer in bullet points", "2026-01-01", "read on the phone", [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", FactTable.Rule, "no meat in recipes", "2026-01-02", "vegetarian", [], None));
        var again = Ok(await _memory.RecordFactAsync(_user, Agent, "the person", FactTable.Rule, "answer in bullet points", "2026-01-03", null, [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", FactTable.Open, "send the photos", "2026-01-01", "Friday", [], None));
        Ok(await _memory.RecordFactAsync(_user, Agent, "the person", FactTable.Open, "renew the passport", "2026-01-01", null, [], None));

        Ok(await _memory.EndFactAsync(_user, Agent, "the person", FactTable.Open, "2026-01-05", retract: false, value: "renew the passport", None));

        Assert.Equal(MemoryOperation.Unchanged, again.Operation);
        var current = Ok(await _memory.CurrentFactsAsync(_user, "the person", None));
        Assert.Equal(["answer in bullet points", "no meat in recipes"], current.Where(f => f.Attribute == FactTable.Rule).Select(f => f.Value).Order());
        Assert.Equal(["send the photos"], current.Where(f => f.Attribute == FactTable.Open).Select(f => f.Value));
    }

    [Fact]
    public async Task RecordFactAsync_WhenTheSourceIsPrivate_ShouldNotCopyItsWordsIntoTheFacts()
    {
        var said = await _memory.RecordAsync(_user, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Chat, "A private thing about Ada.", Sensitivity: MemorySensitivity.Private), None);

        Ok(await _memory.RecordFactAsync(_user, Agent, "Ada", "city", "Porto", null, null, [said.Id], None));

        Assert.Equal(string.Empty, Assert.Single(Ok(await _memory.CurrentFactsAsync(_user, "Ada", None))).Quote);
    }
}
