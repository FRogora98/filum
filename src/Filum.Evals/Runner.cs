using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Filum.Evals;

public sealed record CheckedExpectation(int Turn, ExpectationResult Result);

/// <summary>One scenario played once by one model.</summary>
public sealed record ScenarioRun(
    string ScenarioId,
    string Model,
    int Repetition,
    IReadOnlyList<TurnRecord> Turns,
    IReadOnlyList<CheckedExpectation> Checks,
    IReadOnlyList<int> ClaimedNotDone,
    decimal JudgeCostUsd,
    IReadOnlyList<MemorySnapshot>? Memories = null,
    string? SetupError = null)
{
    public bool Succeeded => SetupError is null && Checks.All(c => c.Result.Passed) && Turns.All(t => t.Error is null || t.Refused && ExpectsRefusal(t.Index));

    public decimal CostUsd => Turns.Sum(t => t.CostUsd);

    private bool ExpectsRefusal(int turn) => Checks.Any(c => c.Turn == turn && c.Result.Expectation.Type == "refused");
}

/// <summary>
/// Where the scenarios are played: a service hosting the engine, the prefix it mapped the groups under, how a new
/// synthetic person is made (registering an account, or a header with a new id), and an optional command of the host's
/// own run after the person exists (<c>{email}</c> and <c>{person}</c> replaced).
/// </summary>
public sealed record HostTarget(string Prefix = "/api", string? Register = "/api/auth/register", string? PersonHeader = null, string? Setup = null)
{
    public const string HostModel = "host";

    public static HostTarget Stock { get; } = new();

    public string Describe(Uri? service) =>
        $"{service}{Prefix.TrimStart('/')} · {(PersonHeader is null ? $"register {Register}" : $"header {PersonHeader}")}{(Setup is null ? "" : " · setup")}";
}

/// <summary>A synthetic person of one run and how their requests say who they are.</summary>
public sealed record EvalPerson(Guid Id, string Email, string? Token, string? Header)
{
    public void Apply(HttpRequestMessage request)
    {
        if (Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        if (Header is not null)
        {
            request.Headers.Add(Header, Id.ToString());
        }
    }
}

/// <summary>The common auth contract (email and password in, an access token out), as the hosts that register accounts share it.</summary>
public sealed record RegisterRequest(string Email, string Password);

public sealed record RegisteredSession(string AccessToken);

/// <summary>
/// Plays a scenario against a running host through its public API only: a new person for every run, one conversation
/// per chat label, the model under test named in every message (or none, for the host's own). After each turn it reads
/// the person's memory, checks the turn's expectations and asks the judge whether the answer claims a change.
/// </summary>
public sealed class ScenarioRunner(HttpClient service, IClaimJudge judge, HostTarget? target = null) : IScenarioRunner
{
    private readonly HostTarget _target = target ?? HostTarget.Stock;

    public async Task<ScenarioRun> RunAsync(Scenario scenario, string model, int repetition, CancellationToken cancellationToken)
    {
        var person = await CreatePersonAsync(cancellationToken);
        if (_target.Setup is not null && await SetupCommand.RunAsync(_target.Setup, person, cancellationToken) is { } setupError)
        {
            return new ScenarioRun(scenario.Id, model, repetition, [], [], [], 0m, SetupError: setupError);
        }

        var conversations = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var turns = new List<TurnRecord>();
        var checks = new List<CheckedExpectation>();
        var claimed = new List<int>();
        var judgeCost = 0m;
        var memories = new List<MemorySnapshot>();

        for (var i = 0; i < scenario.Turns.Count; i++)
        {
            var step = scenario.Turns[i];
            if (!conversations.TryGetValue(step.Chat, out var conversation))
            {
                conversations[step.Chat] = conversation = Guid.NewGuid();
            }

            var reads = step.Expect.Where(e => e.Type == "host_unchanged").Select(e => e.Get!).Distinct().ToList();
            var before = new Dictionary<string, string?>();
            foreach (var path in reads)
            {
                before[path] = await ReadRawAsync(person, path, cancellationToken);
            }

            var turn = await SendAsync(person, conversation, i + 1, step, model, cancellationToken);
            if (reads.Count > 0)
            {
                var unchanged = new Dictionary<string, bool>();
                foreach (var path in reads)
                {
                    var after = await ReadRawAsync(person, path, cancellationToken);
                    if (before[path] is not null && after is not null)
                    {
                        unchanged[path] = Same(before[path]!, after);
                    }
                }

                turn = turn with { Unchanged = unchanged };
            }

            if (turn.Refused)
            {
                turn = turn with { ConversationExists = await ConversationExistsAsync(person, conversation, cancellationToken) };
            }

            turns.Add(turn);
            var memory = await ReadMemoryAsync(person, cancellationToken);
            memories.Add(memory);
            var earlier = turns[..^1];
            foreach (var expectation in step.Expect)
            {
                if (expectation.Type == "judge")
                {
                    var (result, cost) = await JudgeAsync(expectation, turn, cancellationToken);
                    judgeCost += cost;
                    checks.Add(new CheckedExpectation(i + 1, result));
                }
                else
                {
                    checks.Add(new CheckedExpectation(i + 1, Expectations.Check(expectation, turn, memory, earlier)));
                }
            }

            if (turn.Error is null && turn.Answer.Length > 0)
            {
                var verdict = await judge.ClaimsChangeAsync(turn.Answer, cancellationToken);
                judgeCost += verdict.CostUsd;
                if (verdict.ClaimsChange && !turn.Wrote)
                {
                    claimed.Add(i + 1);
                }
            }
        }

        // The memory after each turn is kept, so a fix to a check can be scored again without running the models again.
        return new ScenarioRun(scenario.Id, model, repetition, turns, checks, claimed, judgeCost, memories);
    }

    private async Task<(ExpectationResult Result, decimal Cost)> JudgeAsync(Expectation e, TurnRecord turn, CancellationToken cancellationToken)
    {
        if (turn.Error is not null || turn.Answer.Length == 0)
        {
            return (new ExpectationResult(e, false, turn.Error is null ? "no answer to judge" : $"the turn failed: {turn.Error}"), 0m);
        }

        var asked = await judge.AskAsync(e.Question!, turn.Answer, cancellationToken);
        var said = asked.Yes ? "yes" : "no";
        return (new ExpectationResult(e, said == e.PassIf, $"the judge said {said}"), asked.CostUsd);
    }

    /// <summary>A new synthetic person for one run: a registered account, or a new id for the host's header.</summary>
    private async Task<EvalPerson> CreatePersonAsync(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var email = $"eval-{id:N}@example.invalid";
        if (_target.PersonHeader is not null)
        {
            return new EvalPerson(id, email, null, _target.PersonHeader);
        }

        using var response = await service.PostAsJsonAsync(_target.Register, new RegisterRequest(email, $"eval-{Guid.NewGuid():N}-1"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Registering a synthetic person at {_target.Register} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        return new EvalPerson(id, email, (await response.Content.ReadFromJsonAsync<RegisteredSession>(cancellationToken))!.AccessToken, null);
    }

    private async Task<TurnRecord> SendAsync(EvalPerson person, Guid conversation, int index, ScenarioTurn step, string model, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url($"/conversations/{conversation}/messages"))
        {
            Content = JsonContent.Create(new SendMessageRequest(Guid.NewGuid(), step.Message, model == HostTarget.HostModel ? null : model))
        };
        person.Apply(request);

        var clock = Stopwatch.StartNew();
        using var response = await service.SendAsync(request, cancellationToken);
        var seconds = clock.Elapsed.TotalSeconds;
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TurnRecord(index, step.Chat, step.Message, string.Empty, [], 0, 0, 0, seconds, $"{(int)response.StatusCode} {problem}", Status: (int)response.StatusCode);
        }

        var turn = (await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken))!;
        return new TurnRecord(index, step.Chat, step.Message, turn.AssistantMessage.Content, turn.AssistantMessage.Steps,
            turn.Usage?.InputTokens ?? 0, turn.Usage?.OutputTokens ?? 0, turn.Usage?.CostUsd ?? 0, seconds, Proposal: turn.AssistantMessage.Proposal, Status: (int)response.StatusCode);
    }

    private async Task<MemorySnapshot> ReadMemoryAsync(EvalPerson person, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/memory/files"));
        person.Apply(request);
        using var response = await service.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            return MemorySnapshot.Unavailable;
        }

        var files = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<MemoryFileDto>>(cancellationToken) ?? [] : [];
        var details = new List<MemoryFileDetailDto>();
        foreach (var file in files)
        {
            if (await GetAsync<MemoryFileDetailDto>(person, Url($"/memory/file?path={Uri.EscapeDataString(file.Path)}"), cancellationToken) is { } detail)
            {
                details.Add(detail);
            }
        }

        return new MemorySnapshot(details);
    }

    private async Task<bool> ConversationExistsAsync(EvalPerson person, Guid conversation, CancellationToken cancellationToken) =>
        (await GetAsync<List<ConversationDto>>(person, Url("/conversations"), cancellationToken) ?? []).Any(c => c.Id == conversation);

    /// <summary>A host path as the person reads it, for <c>host_unchanged</c>; null when it cannot be read.</summary>
    private async Task<string?> ReadRawAsync(EvalPerson person, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        person.Apply(request);
        using var response = await service.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
    }

    /// <summary>The same JSON, whatever the formatting; the same text when it is not JSON.</summary>
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

    private string Url(string path) => $"{_target.Prefix.TrimEnd('/')}{path}";

    private async Task<T?> GetAsync<T>(EvalPerson person, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        person.Apply(request);
        using var response = await service.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(cancellationToken) : default;
    }
}

/// <summary>The host's own command after a person is made, through the system shell; its failure fails the run.</summary>
public static class SetupCommand
{
    public static async Task<string?> RunAsync(string command, EvalPerson person, CancellationToken cancellationToken)
    {
        var line = command.Replace("{email}", person.Email, StringComparison.Ordinal).Replace("{person}", person.Id.ToString(), StringComparison.Ordinal);
        // cmd reads its command line raw: quoting it as an argument would turn the command's own quotes into \".
        var start = OperatingSystem.IsWindows() ? new ProcessStartInfo("cmd") { Arguments = $"/c {line}" } : new ProcessStartInfo("sh", ["-c", line]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The setup command could not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? null : $"setup exited with {process.ExitCode}: {(await output + await error).Trim()}";
    }
}
