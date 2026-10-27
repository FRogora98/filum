namespace Filum.Evals.LongMemEval.Contracts;

/// <summary>A system under test: plays one question on the protocol and gives its answer.</summary>
internal interface ILongMemEvalSystemStrategy
{
    string Name { get; }

    string Model { get; }

    /// <summary>How many agent sessions a question takes (for the session cap); zero when it uses none.</summary>
    int SessionsFor(LongMemEvalInstance instance) => 0;

    /// <summary>The model tokens a question is expected to take, for the estimate before a run: the question alone by default.</summary>
    (int Input, int Output) EstimatedTokens(LongMemEvalInstance instance) => (LongMemEvalEstimate.PromptTokens, LongMemEvalEstimate.OutputTokensPerTurn);

    Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken);
}
