using Filum.Evals.LongMemEval;
using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.UnitTests.Fakes;

internal sealed class FixedSystem(string name, int sessions) : ILongMemEvalSystemStrategy
{
    public string Name => name;

    public string Model => "fixed";

    public int SessionsFor(LongMemEvalInstance instance) => sessions;

    public Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken) =>
        Task.FromResult(new LongMemEvalAnswer("an answer", 10, 1, 0.001m, 0.1, 2));
}
