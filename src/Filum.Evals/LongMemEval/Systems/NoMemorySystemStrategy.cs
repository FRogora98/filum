using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.LongMemEval.Systems;

internal sealed class NoMemorySystemStrategy(DirectModel model) : ILongMemEvalSystemStrategy
{
    public string Name => "no memory";

    public string Model => model.Id;

    public Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken) =>
        DirectAnswer.AskAsync(model, LongMemEvalProtocol.Question(instance), cancellationToken);
}
