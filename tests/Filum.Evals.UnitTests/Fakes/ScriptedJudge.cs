using Filum.Evals.Judging;

namespace Filum.Evals.UnitTests.Fakes;

// Claims a change when the answer says "saved"; each call costs a fixed amount.
internal sealed class ScriptedJudge : IClaimJudge
{
    public const decimal Cost = 0.0001m;

    public Func<string, string, bool> Yes { get; init; } = (_, _) => false;

    public List<string> Questions { get; } = [];

    public Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken) =>
        Task.FromResult(new ClaimVerdict(answer.Contains("saved", StringComparison.OrdinalIgnoreCase), Cost));

    public Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken)
    {
        Questions.Add(question);
        return Task.FromResult(new JudgeAnswer(Yes(question, answer), Cost));
    }
}
