using Filum.Evals.Checks;
using Filum.Evals.Judging;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Runs;

// The same way in every runner: a judge expectation through the judge, the others in code, then whether the answer
// claims a change that was not made.
internal sealed class TurnAssessor(IClaimJudge judge)
{
    public async Task<TurnAssessment> AssessAsync(ScenarioTurn step, TurnRecord turn, MemorySnapshot memory, IReadOnlyList<TurnRecord> earlier, CancellationToken cancellationToken)
    {
        var checks = new List<CheckedExpectation>();
        var cost = 0m;
        foreach (var expectation in step.Expect)
        {
            if (expectation.Type == ExpectationTypes.Judge)
            {
                var (result, judged) = await JudgeAsync(expectation, turn, cancellationToken);
                cost += judged;
                checks.Add(new CheckedExpectation(turn.Index, result));
            }
            else
            {
                checks.Add(new CheckedExpectation(turn.Index, ExpectationChecker.Check(expectation, turn, memory, earlier)));
            }
        }

        var (claimed, claimCost) = await ClaimedButNotDoneAsync(turn, cancellationToken);
        return new TurnAssessment(checks, claimed, cost + claimCost);
    }

    private async Task<(ExpectationResult Result, decimal Cost)> JudgeAsync(Expectation e, TurnRecord turn, CancellationToken cancellationToken)
    {
        if (turn.Error is not null || turn.Answer.Length == 0)
        {
            return (new ExpectationResult(e, false, turn.Error is null ? "no answer to judge" : $"the turn failed: {turn.Error}"), 0m);
        }

        if (e.Question is not { } question)
        {
            return (new ExpectationResult(e, false, "no question for the judge"), 0m);
        }

        var asked = await judge.AskAsync(question, turn.Answer, cancellationToken);
        var said = asked.Yes ? ExpectationTypes.Yes : ExpectationTypes.No;
        return (new ExpectationResult(e, said == e.PassIf, $"the judge said {said}"), asked.CostUsd);
    }

    private async Task<(bool Claimed, decimal Cost)> ClaimedButNotDoneAsync(TurnRecord turn, CancellationToken cancellationToken)
    {
        if (turn.Error is not null || turn.Answer.Length == 0)
        {
            return (false, 0m);
        }

        var verdict = await judge.ClaimsChangeAsync(turn.Answer, cancellationToken);
        return (verdict.ClaimsChange && !turn.Wrote, verdict.CostUsd);
    }
}
