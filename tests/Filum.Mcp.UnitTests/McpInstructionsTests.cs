using Filum.Mcp.UnitTests.Builders;

namespace Filum.Mcp.UnitTests;

public sealed class McpInstructionsTests : IDisposable
{
    private readonly ServerBuilder _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void Text_WhenItSpeaksOfTidying_ShouldAskForItOnlyAfterThePersonWasAnswered()
    {
        var line = McpInstructions.Text.Split('\n').Single(l => l.Contains("call memory_consolidate once", StringComparison.Ordinal));

        Assert.DoesNotContain("start of a session", line, StringComparison.Ordinal);
        Assert.Contains("After you answered", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_WhenItSpeaksOfFacts_ShouldAskForEveryPointFactThatCanChangeOnePerSubjectAndAttributeWithTheNewValueAsARevision()
    {
        var line = McpInstructions.Text.Split('\n').Single(l => l.Contains("recorded with fact_record", StringComparison.Ordinal));

        Assert.Contains("A point fact that can change, about a person, a place or a thing", line, StringComparison.Ordinal);
        Assert.Contains("one per subject and attribute", line, StringComparison.Ordinal);
        Assert.Contains("a revision of the old one", line, StringComparison.Ordinal);
        Assert.Contains("never renumber", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldLimitARuleToStyleAndFormatAndAskToAbstainAndToSaveWithoutBeingAsked()
    {
        Assert.Contains("of style and format (length, layout, units, language) in every answer; never safety or the host's own rules", McpInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("never fill a person's relation, name or value in from something else", McpInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Save without being asked, with the tools that keep it: a promise or a plan with a day", McpInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("a list the person asked you to keep (a collection or a document). memory_log keeps only their words.", Filum.Mcp.Tools.ConsolidationRules.Text, StringComparison.Ordinal);
        Assert.Contains("End an open item only on an event that says, in its words, it is done or dropped", Filum.Mcp.Tools.ConsolidationRules.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSaveASkillTheyAskForAtOnceAndComputeCountsAndTotals()
    {
        var skills = McpInstructions.Text.Split('\n').Single(l => l.Contains("skill_use", StringComparison.Ordinal));

        Assert.Contains("A skill they ask you to create is saved with skill_save at once, never skill_propose", skills, StringComparison.Ordinal);
        Assert.Contains("offer one with skill_propose and save it only on yes", skills, StringComparison.Ordinal);
        Assert.Contains("a message starts with /<its name>, call skill_use", skills, StringComparison.Ordinal);
        Assert.Contains("Counts and totals over rows come from collection_aggregate or compute_run", McpInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("For how many or how much in total, list with every (a category | its members) before answering, and count each item it shows once, unsure ones too, within the time asked.", McpInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Fit_WhenTheRulesAreLongerThanTheRoom_ShouldKeepWholeLinesOrNothing()
    {
        const string rules = "# Rules the person set: follow every one in this answer\n\n- end every answer with 'Onward.'\n- temperatures in Celsius";

        Assert.Equal(rules, McpInstructions.Fit(rules, 500));
        Assert.Equal("# Rules the person set: follow every one in this answer\n\n- end every answer with 'Onward.'", McpInstructions.Fit(rules, 100));
        Assert.Equal(string.Empty, McpInstructions.Fit(rules, 60));
        Assert.Equal(string.Empty, McpInstructions.Fit(string.Empty, 500));
    }

    [Fact]
    public void ConsolidateDescription_ShouldNotAskForItAtTheStartOfASession()
    {
        Assert.DoesNotContain("start of a session", _server.ConsolidateToolStrategy().Function().Description, StringComparison.Ordinal);
    }
}
