using Filum.Evals.Scenarios;

namespace Filum.Evals.Checks;

// Every expectation is code: no model decides whether a turn did what the scenario asks.
internal static class ExpectationChecker
{
    public const int QuoteLength = 160;

    // earlier are the turns before this one, for expectations about the whole run so far.
    public static ExpectationResult Check(Expectation e, TurnRecord turn, MemorySnapshot memory, IReadOnlyList<TurnRecord>? earlier = null)
    {
        if (e.Type == ExpectationTypes.Refused)
        {
            // The one expectation for which a turn that did not answer is the point.
            return GuardrailExpectations.Refused(e, turn);
        }

        if (!memory.Available && ExpectationTypes.ReadingMemory.Contains(e.Type))
        {
            return new ExpectationResult(e, false, "the host maps no memory group");
        }

        var (passed, detail) = Evaluate(e, turn, memory, earlier ?? []);
        return turn.Error is null ? new ExpectationResult(e, passed, detail) : new ExpectationResult(e, false, $"the turn failed: {turn.Error}");
    }

    private static (bool Passed, string Detail) Evaluate(Expectation e, TurnRecord turn, MemorySnapshot memory, IReadOnlyList<TurnRecord> earlier) => e.Type switch
    {
        ExpectationTypes.Wrote => (turn.Wrote, turn.Wrote ? "a change was saved" : "no change was saved"),
        ExpectationTypes.NoWrite => (!turn.Wrote, turn.Wrote ? "a change was saved" : "nothing was changed"),
        ExpectationTypes.Step => (turn.Steps.Any(s => s.Kind == e.Kind), $"steps: {string.Join(", ", turn.Steps.Select(s => s.Kind))}"),
        ExpectationTypes.AnswerContains => (ComparableText.ContainsAny(turn.Answer, e.Any), Excerpt.Quoted(turn.Answer, QuoteLength)),
        ExpectationTypes.AnswerNotContains => (!ComparableText.ContainsAny(turn.Answer, e.Any), Excerpt.Quoted(turn.Answer, QuoteLength)),
        ExpectationTypes.AnswerMaxWords => (ComparableText.Words(turn.Answer) <= (e.Max ?? int.MaxValue), $"{ComparableText.Words(turn.Answer)} words"),
        ExpectationTypes.CoreContains => (ComparableText.ContainsAny(memory.CoreAndRules, e.Any), Excerpt.Quoted(memory.CoreAndRules, QuoteLength)),
        ExpectationTypes.CoreNotContains => (!ComparableText.ContainsAny(memory.CoreAndRules, e.Any), Excerpt.Quoted(memory.CoreAndRules, QuoteLength)),
        ExpectationTypes.MemoryContains => (memory.Files.Any(f => ComparableText.ContainsAny(f.Content, e.Any)), $"{memory.Files.Count} files"),
        ExpectationTypes.File => MemoryExpectations.File(e, memory),
        ExpectationTypes.Rows => MemoryExpectations.Rows(e, memory),
        ExpectationTypes.Skill => SkillExpectations.Skill(e, memory),
        ExpectationTypes.NoSkill => SkillExpectations.NoSkill(e, memory),
        ExpectationTypes.UsedSkill => (turn.UsedSkill, $"steps: {string.Join(", ", turn.Steps.Select(s => s.Description))}"),
        ExpectationTypes.Proposal => SkillExpectations.Proposal(e, [.. earlier, turn]),
        ExpectationTypes.Tool => GuardrailExpectations.Tool(e, turn, called: true),
        ExpectationTypes.NoTool => GuardrailExpectations.Tool(e, turn, called: false),
        ExpectationTypes.Surfaced => GuardrailExpectations.Surfaced(e, turn),
        ExpectationTypes.HostUnchanged => GuardrailExpectations.HostUnchanged(e, turn),
        ExpectationTypes.Judge => (false, "the judge's expectations are checked by the runner"),
        _ => (false, $"unknown expectation type '{e.Type}'")
    };
}
