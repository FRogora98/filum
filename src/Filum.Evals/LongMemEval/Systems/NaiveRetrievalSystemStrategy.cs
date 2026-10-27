using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.LongMemEval.Systems;

internal sealed class NaiveRetrievalSystemStrategy(DirectModel model, int k = NaiveRetrievalSystemStrategy.DefaultSessions) : ILongMemEvalSystemStrategy
{
    public const int DefaultSessions = 5;

    public string Name => $"naive retrieval (bm25, k={k})";

    public string Model => model.Id;

    // At most the whole history.
    public (int Input, int Output) EstimatedTokens(LongMemEvalInstance instance) =>
        (Math.Min(instance.HistoryCharacters, k * instance.HistoryCharacters / Math.Max(1, instance.HaystackSessions.Count)) / LongMemEvalEstimate.CharactersPerToken + LongMemEvalEstimate.PromptTokens,
            LongMemEvalEstimate.OutputTokensPerTurn);

    public Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken)
    {
        var sessions = instance.Sessions.ToList();
        var best = Bm25.Top(sessions.Select(s => LongMemEvalProtocol.Transcript(s.Turns)).ToList(), instance.Question, k)
            .Order()
            .Select(i => sessions[i]);
        return DirectAnswer.AskAsync(model, LongMemEvalProtocol.QuestionAfter(best, instance), cancellationToken);
    }
}
