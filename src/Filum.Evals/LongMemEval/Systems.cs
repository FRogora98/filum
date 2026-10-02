using Microsoft.Extensions.AI;
using OpenAI;
using ChatClient = OpenAI.Chat.ChatClient;
using System.ClientModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Filum.Evals.LongMemEval;

/// <summary>What a system answered to one question, and what it took (model tokens and dollars; agent sessions apart).</summary>
public sealed record LmeAnswer(string Answer, int InputTokens, int OutputTokens, decimal CostUsd, double Seconds, int Turns, string? Error = null, string? ModelVersion = null)
{
    public static LmeAnswer Failed(string error, double seconds = 0, int turns = 0) => new(string.Empty, 0, 0, 0, seconds, turns, error);
}

/// <summary>A system under test: plays one question on the protocol and gives its answer.</summary>
public interface ILmeSystem
{
    string Name { get; }

    string Model { get; }

    /// <summary>How many agent sessions a question takes (for the session cap); zero when it uses none.</summary>
    int SessionsFor(LmeInstance instance) => 0;

    Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken);
}

/// <summary>
/// Filum on a host (spec 020): a new person per question, each session a new chat in date order (in parts when long),
/// the question in one more. The host's configuration makes the system (the claim check on or off, revisions or not),
/// and the run names it.
/// </summary>
/// <param name="consolidate">After each session, a consolidation pass through the host's <c>memory/consolidate</c> (spec 030), as a quiet conversation would get.</param>
public sealed class HostLmeSystem(HttpClient service, HostTarget target, string model, string name, bool consolidate = false) : ILmeSystem
{
    public string Name => name;

    public string Model => model;

    public async Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var person = await CreatePersonAsync(cancellationToken);
        if (target.Setup is not null && await SetupCommand.RunAsync(target.Setup, person, cancellationToken) is { } setupError)
        {
            return LmeAnswer.Failed(setupError);
        }

        var (input, output, cost, turns) = (0, 0, 0m, 0);
        foreach (var (date, sessionTurns) in instance.Sessions)
        {
            var conversation = Guid.NewGuid();
            foreach (var message in LmeProtocol.SessionMessages(date, sessionTurns))
            {
                var sent = await SendAsync(person, conversation, message, cancellationToken);
                turns++;
                if (sent.Error is not null)
                {
                    return LmeAnswer.Failed($"session of {date}: {sent.Error}", clock.Elapsed.TotalSeconds, turns) with { InputTokens = input, OutputTokens = output, CostUsd = cost };
                }

                (input, output, cost) = (input + sent.Input, output + sent.Output, cost + sent.Cost);
            }

            if (consolidate)
            {
                // A pass that fails leaves its events for the next one, as in the product: the question goes on.
                var pass = await ConsolidateAsync(person, cancellationToken);
                (input, output, cost) = (input + pass.Input, output + pass.Output, cost + pass.Cost);
            }
        }

        var answer = await SendAsync(person, Guid.NewGuid(), LmeProtocol.Question(instance), cancellationToken);
        turns++;
        return new LmeAnswer(answer.Text, input + answer.Input, output + answer.Output, cost + answer.Cost, clock.Elapsed.TotalSeconds, turns, answer.Error);
    }

    private sealed record Sent(string Text, int Input, int Output, decimal Cost, string? Error);

    private async Task<Sent> SendAsync(EvalPerson person, Guid conversation, string content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{target.Prefix.TrimEnd('/')}/conversations/{conversation}/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest(Guid.NewGuid(), content, model == HostTarget.HostModel ? null : model))
        };
        person.Apply(request);
        using var response = await service.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new Sent(string.Empty, 0, 0, 0, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var turn = (await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken))!;
        return new Sent(turn.AssistantMessage.Content, turn.Usage?.InputTokens ?? 0, turn.Usage?.OutputTokens ?? 0, turn.Usage?.CostUsd ?? 0, null);
    }

    private sealed record Pass(int Events, int Changes, int Proposals, UsageDto Usage);

    private async Task<Sent> ConsolidateAsync(EvalPerson person, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{target.Prefix.TrimEnd('/')}/memory/consolidate");
        person.Apply(request);
        using var response = await service.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new Sent(string.Empty, 0, 0, 0, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var pass = (await response.Content.ReadFromJsonAsync<Pass>(cancellationToken))!;
        return new Sent(string.Empty, pass.Usage.InputTokens, pass.Usage.OutputTokens, pass.Usage.CostUsd, null);
    }

    private async Task<EvalPerson> CreatePersonAsync(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var email = $"eval-{id:N}@example.invalid";
        if (target.PersonHeader is not null)
        {
            return new EvalPerson(id, email, null, target.PersonHeader);
        }

        using var response = await service.PostAsJsonAsync(target.Register, new RegisterRequest(email, $"eval-{Guid.NewGuid():N}-1"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return new EvalPerson(id, email, (await response.Content.ReadFromJsonAsync<RegisteredSession>(cancellationToken))!.AccessToken, null);
    }
}

/// <summary>A model called directly, for the references: its client and its prices.</summary>
public sealed record DirectModel(string Id, IChatClient Client, decimal InputPricePerMillionUsd, decimal OutputPricePerMillionUsd)
{
    public decimal Cost(UsageDetails? usage) =>
        ((usage?.InputTokenCount ?? 0) * InputPricePerMillionUsd + (usage?.OutputTokenCount ?? 0) * OutputPricePerMillionUsd) / 1_000_000m;

    /// <summary>
    /// The models of a host's configuration file (its <c>Models</c> and <c>Providers</c> sections), each called through
    /// its provider's OpenAI-compatible API with the key in <c>&lt;PROVIDER&gt;_API_KEY</c>.
    /// </summary>
    public static DirectModel Load(string configurationFile, string id)
    {
        var root = JsonDocument.Parse(File.ReadAllText(configurationFile), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;
        var model = root.GetProperty("Models").EnumerateArray().FirstOrDefault(m => m.GetProperty("Id").GetString() == id);
        if (model.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"The model '{id}' is not in {configurationFile}.");
        }

        var provider = model.TryGetProperty("Provider", out var p) ? p.GetString()! : "openai";
        var providerModel = model.TryGetProperty("ProviderModel", out var pm) && pm.GetString() is { Length: > 0 } name ? name : id;
        var baseUrl = root.TryGetProperty("Providers", out var providers) && providers.TryGetProperty(provider, out var settings) && settings.TryGetProperty("BaseUrl", out var url) ? url.GetString() : null;
        var key = DotEnv.Find($"{provider.ToUpperInvariant()}_API_KEY") ?? throw new InvalidOperationException($"{provider.ToUpperInvariant()}_API_KEY is needed for {id}.");
        var options = string.IsNullOrWhiteSpace(baseUrl) ? new OpenAIClientOptions() : new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        var client = new ChatClient(providerModel, new ApiKeyCredential(key), options).AsIChatClient();
        return new DirectModel(id, client, model.GetProperty("InputPricePerMillionUsd").GetDecimal(), model.GetProperty("OutputPricePerMillionUsd").GetDecimal());
    }
}

/// <summary>The reference with no memory: the question alone.</summary>
public sealed class NoMemorySystem(DirectModel model) : ILmeSystem
{
    public string Name => "no memory";

    public string Model => model.Id;

    public Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken) =>
        DirectAnswer.AskAsync(model, LmeProtocol.Question(instance), cancellationToken);
}

/// <summary>The naive-retrieval reference: the <paramref name="k"/> sessions that best match the question (BM25), then the question.</summary>
public sealed class NaiveRetrievalSystem(DirectModel model, int k = 5) : ILmeSystem
{
    public string Name => $"naive retrieval (bm25, k={k})";

    public string Model => model.Id;

    public Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken)
    {
        var sessions = instance.Sessions.ToList();
        var best = Bm25.Top(sessions.Select(s => LmeProtocol.Transcript(s.Turns)).ToList(), instance.Question, k)
            .Order()
            .Select(i => sessions[i]);
        var prompt = $"Here are past conversations with the person:\n\n{LmeProtocol.History(best)}\n\n{LmeProtocol.Question(instance)}";
        return DirectAnswer.AskAsync(model, prompt, cancellationToken);
    }
}

internal static class DirectAnswer
{
    public static async Task<LmeAnswer> AskAsync(DirectModel model, string prompt, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var response = await model.Client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
            return new LmeAnswer(response.Text.Trim(), (int)(response.Usage?.InputTokenCount ?? 0), (int)(response.Usage?.OutputTokenCount ?? 0), model.Cost(response.Usage), clock.Elapsed.TotalSeconds, 1);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return LmeAnswer.Failed($"{exception.GetType().Name}: {exception.Message}", clock.Elapsed.TotalSeconds, 1);
        }
    }
}

/// <summary>Okapi BM25 over whole documents, with the usual k1 = 1.2 and b = 0.75; words are lower-case letters and digits.</summary>
public static class Bm25
{
    public static IReadOnlyList<int> Top(IReadOnlyList<string> documents, string query, int k)
    {
        var docs = documents.Select(Words).ToList();
        var average = docs.Count == 0 ? 0 : docs.Average(d => d.Count);
        var terms = Words(query).Distinct().ToList();
        var frequency = terms.ToDictionary(t => t, t => docs.Count(d => d.Contains(t)));
        return docs
            .Select((d, i) => (Index: i, Score: terms.Sum(t =>
            {
                var tf = d.Count(w => w == t);
                if (tf == 0)
                {
                    return 0.0;
                }

                var idf = Math.Log(1 + (docs.Count - frequency[t] + 0.5) / (frequency[t] + 0.5));
                return idf * tf * 2.2 / (tf + 1.2 * (1 - 0.75 + 0.75 * d.Count / Math.Max(1, average)));
            })))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Index)
            .Take(k)
            .Select(s => s.Index)
            .ToList();
    }

    public static List<string> Words(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), "[\\p{L}\\p{N}]+").Select(m => m.Value).ToList();
}

/// <summary>
/// A large model through Claude Code (the owner's subscription): with filum-mcp (each session a new agent session, the
/// question in one more), with no memory (the question alone), or with the whole history in its prompt.
/// </summary>
public sealed class ClaudeLmeSystem(IAgent agent, ClaudeLmeSystem.Mode mode, string filumMcp, string model) : ILmeSystem
{
    public enum Mode
    {
        WithFilum,
        NoMemory,
        FullContext
    }

    public string Name => mode switch
    {
        Mode.WithFilum => "claude code + filum-mcp",
        Mode.NoMemory => "claude code, no memory",
        _ => "claude code, whole history in the prompt"
    };

    public string Model => model;

    public int SessionsFor(LmeInstance instance) => mode == Mode.WithFilum ? instance.HaystackSessions.Count + 1 : 1;

    public async Task<LmeAnswer> AnswerAsync(LmeInstance instance, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var root = Path.Combine(Path.GetTempPath(), "filum-evals-lme", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var config = Path.Combine(root, "mcp.json");
        var runner = new McpScenarioRunner(agent, NullJudge.Instance, filumMcp, new SessionBudget(int.MaxValue));
        await File.WriteAllTextAsync(config, runner.McpConfig(mode == Mode.WithFilum, Path.Combine(root, "memory")), cancellationToken);
        try
        {
            var turns = 0;
            if (mode == Mode.WithFilum)
            {
                foreach (var (date, sessionTurns) in instance.Sessions)
                {
                    var work = Directory.CreateDirectory(Path.Combine(root, $"session-{++turns}")).FullName;
                    var ingested = await agent.AskAsync($"{LmeProtocol.Header(date)}\n\n{LmeProtocol.Transcript(sessionTurns)}", work, config, filumMounted: true, cancellationToken);
                    if (ingested.Error is not null)
                    {
                        return LmeAnswer.Failed($"session of {date}: {ingested.Error}", clock.Elapsed.TotalSeconds, turns);
                    }
                }
            }

            var prompt = mode == Mode.FullContext
                ? $"Here are past conversations with the person:\n\n{LmeProtocol.History(instance.Sessions)}\n\n{LmeProtocol.Question(instance)}"
                : LmeProtocol.Question(instance);
            var asked = await agent.AskAsync(prompt, Directory.CreateDirectory(Path.Combine(root, "question")).FullName, config, mode == Mode.WithFilum, cancellationToken);
            turns++;
            return new LmeAnswer(asked.Answer, 0, 0, 0m, clock.Elapsed.TotalSeconds, turns, asked.Error, asked.Model);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A server that has not exited yet may still hold a file: the temporary folder is left to the system.
            }
        }
    }

    private sealed class NullJudge : IClaimJudge
    {
        public static NullJudge Instance { get; } = new();

        public Task<ClaimVerdict> ClaimsChangeAsync(string answer, CancellationToken cancellationToken) => Task.FromResult(new ClaimVerdict(false, 0));

        public Task<JudgeAnswer> AskAsync(string question, string answer, CancellationToken cancellationToken) => Task.FromResult(new JudgeAnswer(false, 0));
    }
}
