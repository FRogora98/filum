using Filum.Evals.Hosts;
using System.Diagnostics;
using Filum.Evals.LongMemEval.Contracts;

namespace Filum.Evals.LongMemEval.Systems;

// The host's configuration makes the system (the claim check on or off, revisions or not), and the run names it. With
// consolidate, a consolidation pass follows each session, as a quiet conversation would get.
internal sealed class HostSystemStrategy(HttpClient service, HostTarget target, string model, string name, bool consolidate = false) : ILongMemEvalSystemStrategy
{
    private readonly HostClient _host = new(service, target);

    public string Name => name;

    public string Model => model;

    public (int Input, int Output) EstimatedTokens(LongMemEvalInstance instance) => LongMemEvalEstimate.FilumTokens(instance);

    public async Task<LongMemEvalAnswer> AnswerAsync(LongMemEvalInstance instance, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var person = await _host.CreatePersonAsync(cancellationToken);
        if (target.Setup is not null && await SetupCommand.RunAsync(target.Setup, person, cancellationToken) is { } setupError)
        {
            return LongMemEvalAnswer.Failed(setupError);
        }

        var (spent, turns, failure) = await IngestAsync(person, instance, cancellationToken);
        if (failure is not null)
        {
            return LongMemEvalAnswer.Failed(failure, clock.Elapsed.TotalSeconds, turns) with { InputTokens = spent.Input, OutputTokens = spent.Output, CostUsd = spent.Cost };
        }

        var answer = await SendAsync(person, Guid.NewGuid(), LongMemEvalProtocol.Question(instance), cancellationToken);
        return new LongMemEvalAnswer(answer.Text, spent.Input + answer.Input, spent.Output + answer.Output, spent.Cost + answer.Cost, clock.Elapsed.TotalSeconds, turns + 1, answer.Error);
    }

    // Returns what they spent, the messages sent and the first failure.
    private async Task<(Sent Spent, int Turns, string? Failure)> IngestAsync(EvalPerson person, LongMemEvalInstance instance, CancellationToken cancellationToken)
    {
        var (spent, turns) = (Sent.Nothing, 0);
        foreach (var (date, sessionTurns) in instance.Sessions)
        {
            var conversation = Guid.NewGuid();
            foreach (var message in LongMemEvalProtocol.SessionMessages(date, sessionTurns))
            {
                var sent = await SendAsync(person, conversation, message, cancellationToken);
                turns++;
                if (sent.Error is not null)
                {
                    return (spent, turns, $"session of {date}: {sent.Error}");
                }

                spent = spent.Plus(sent);
            }

            if (consolidate)
            {
                // A pass that fails leaves its events for the next one, as in the product: the question goes on.
                spent = spent.Plus(await ConsolidateAsync(person, cancellationToken));
            }
        }

        return (spent, turns, null);
    }

    private sealed record Sent(string Text, int Input, int Output, decimal Cost, string? Error)
    {
        public static Sent Nothing { get; } = new(string.Empty, 0, 0, 0m, null);

        public Sent Plus(Sent other) => this with { Input = Input + other.Input, Output = Output + other.Output, Cost = Cost + other.Cost };
    }

    private async Task<Sent> SendAsync(EvalPerson person, Guid conversation, string content, CancellationToken cancellationToken)
    {
        var reply = await _host.SendMessageAsync(person, conversation, content, model, cancellationToken);
        return reply.Body is { } turn
            ? new Sent(turn.AssistantMessage.Content, turn.Usage?.InputTokens ?? 0, turn.Usage?.OutputTokens ?? 0, turn.Usage?.CostUsd ?? 0, null)
            : new Sent(string.Empty, 0, 0, 0, reply.Error);
    }

    private async Task<Sent> ConsolidateAsync(EvalPerson person, CancellationToken cancellationToken)
    {
        var reply = await _host.ConsolidateAsync(person, cancellationToken);
        return reply.Body is { } pass
            ? new Sent(string.Empty, pass.Usage.InputTokens, pass.Usage.OutputTokens, pass.Usage.CostUsd, null)
            : new Sent(string.Empty, 0, 0, 0, reply.Error);
    }
}
