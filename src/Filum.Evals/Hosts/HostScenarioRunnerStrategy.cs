using Filum.Evals.Checks;
using Filum.Evals.Judging;
using Filum.Evals.Runs;
using Filum.Evals.Scenarios;
using System.Text.Json;
using Filum.Evals.Runs.Contracts;

namespace Filum.Evals.Hosts;

// Through the host's public API only: a new person for every run, one conversation per chat label, the model under test
// named in every message (none for the host's own).
internal sealed class HostScenarioRunnerStrategy(HttpClient service, IClaimJudge judge, HostTarget? target = null) : IScenarioRunnerStrategy
{
    private readonly HostTarget _target = target ?? HostTarget.Stock;
    private readonly HostClient _host = new(service, target ?? HostTarget.Stock);
    private readonly TurnAssessor _assessor = new(judge);

    public async Task<ScenarioRun> RunAsync(Scenario scenario, string model, int repetition, CancellationToken cancellationToken)
    {
        var person = await _host.CreatePersonAsync(cancellationToken);
        if (_target.Setup is not null && await SetupCommand.RunAsync(_target.Setup, person, cancellationToken) is { } setupError)
        {
            return new ScenarioRun(scenario.Id, model, repetition, [], [], [], 0m, SetupError: setupError);
        }

        var conversations = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var recorder = new ScenarioRunRecorder();
        for (var i = 0; i < scenario.Turns.Count; i++)
        {
            var step = scenario.Turns[i];
            var turn = await PlayAsync(person, ConversationOf(conversations, step.Chat), i + 1, step, model, cancellationToken);
            var memory = await _host.ReadMemoryAsync(person, cancellationToken);
            recorder.Record(turn, memory, await _assessor.AssessAsync(step, turn, memory, recorder.Turns, cancellationToken));
        }

        return recorder.ToRun(scenario.Id, model, repetition, withMemories: true);
    }

    private static Guid ConversationOf(Dictionary<string, Guid> conversations, string chat)
    {
        if (!conversations.TryGetValue(chat, out var conversation))
        {
            conversations[chat] = conversation = Guid.NewGuid();
        }

        return conversation;
    }

    // The host paths it must leave alone are read before and after.
    private async Task<TurnRecord> PlayAsync(EvalPerson person, Guid conversation, int index, ScenarioTurn step, string model, CancellationToken cancellationToken)
    {
        var watched = step.Expect.Where(e => e.Type == ExpectationTypes.HostUnchanged).Select(e => e.Get).OfType<string>().Distinct().ToList();
        var before = new Dictionary<string, string?>();
        foreach (var path in watched)
        {
            before[path] = await _host.ReadRawAsync(person, path, cancellationToken);
        }

        var turn = await SendAsync(person, conversation, index, step, model, cancellationToken);
        if (watched.Count > 0)
        {
            turn = turn with { Unchanged = await CompareAsync(person, watched, before, cancellationToken) };
        }

        return turn.Refused
            ? turn with { ConversationExists = await _host.ConversationExistsAsync(person, conversation, cancellationToken) }
            : turn;
    }

    private async Task<TurnRecord> SendAsync(EvalPerson person, Guid conversation, int index, ScenarioTurn step, string model, CancellationToken cancellationToken)
    {
        var reply = await _host.SendMessageAsync(person, conversation, step.Message, model, cancellationToken);
        if (reply.Body is not { } turn)
        {
            return new TurnRecord(index, step.Chat, step.Message, string.Empty, [], 0, 0, 0, reply.Seconds, reply.Error, Status: reply.Status);
        }

        return new TurnRecord(index, step.Chat, step.Message, turn.AssistantMessage.Content, turn.AssistantMessage.Steps,
            turn.Usage?.InputTokens ?? 0, turn.Usage?.OutputTokens ?? 0, turn.Usage?.CostUsd ?? 0, reply.Seconds, Proposal: turn.AssistantMessage.Proposal, Status: reply.Status);
    }

    private async Task<Dictionary<string, bool>> CompareAsync(EvalPerson person, IReadOnlyList<string> watched, Dictionary<string, string?> before, CancellationToken cancellationToken)
    {
        var unchanged = new Dictionary<string, bool>();
        foreach (var path in watched)
        {
            var after = await _host.ReadRawAsync(person, path, cancellationToken);
            if (before[path] is { } earlier && after is not null)
            {
                unchanged[path] = Same(earlier, after);
            }
        }

        return unchanged;
    }

    // Whatever the formatting; the same text when it is not JSON.
    private static bool Same(string before, string after)
    {
        try
        {
            return JsonElement.DeepEquals(JsonDocument.Parse(before).RootElement, JsonDocument.Parse(after).RootElement);
        }
        catch (JsonException)
        {
            return before == after;
        }
    }
}
