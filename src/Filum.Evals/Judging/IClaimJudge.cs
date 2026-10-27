namespace Filum.Evals.Judging;

/// <summary>Decides whether an answer tells the person that something was saved, changed or removed.</summary>
internal interface IClaimJudge
{
    Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken);

    /// <summary>A scenario's own yes/no question about the answer (the <c>judge</c> expectation).</summary>
    Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken);
}
