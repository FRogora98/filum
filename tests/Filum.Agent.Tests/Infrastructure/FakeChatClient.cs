using Microsoft.Extensions.AI;

namespace Filum.Agent.Tests.Infrastructure;

/// <summary>
/// Stands in for the LLM: records what it receives and answers, fails or hangs on demand. With a script of tool
/// calls it asks for those calls first, one batch per model call, so the real function-invocation loop runs the
/// real memory tools; when the script is over it answers with text.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public Func<IReadOnlyList<ChatMessage>, string> Reply { get; set; } = _ => "fake answer";

    public bool Fail { get; set; }

    /// <summary>Fail on the call that would answer with text, after the scripted tool calls have run.</summary>
    public bool FailAfterToolCalls { get; set; }

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Token counts reported with every model call; null to simulate a model that reports none.</summary>
    public UsageDetails? Usage { get; set; } = new() { InputTokenCount = 1000, OutputTokenCount = 500 };

    /// <summary>When set, the scripted tool calls are used only for calls whose messages pass it.</summary>
    public Func<IReadOnlyList<ChatMessage>, bool>? UseToolCallsWhen { get; set; }

    /// <summary>Batches of tool calls the model asks for, in order, before answering.</summary>
    public Queue<FunctionCallContent[]> ToolCalls { get; } = new();

    public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    /// <summary>The verdict the claim check gets, as JSON; the check's calls are recorded apart from the agent's.</summary>
    public Func<IReadOnlyList<ChatMessage>, string> CheckReply { get; set; } = _ => "{\"requested\": false, \"claimed\": false}";

    public List<IReadOnlyList<ChatMessage>> CheckCalls { get; } = [];

    /// <summary>Token counts reported with every check call.</summary>
    public UsageDetails CheckUsage { get; set; } = new() { InputTokenCount = 100, OutputTokenCount = 10 };

    public static FunctionCallContent Call(string name, IDictionary<string, object?> arguments) =>
        new(Guid.NewGuid().ToString("N"), name, arguments);

    /// <summary>Queues one model call that asks for the given tool calls.</summary>
    public FakeChatClient Then(params FunctionCallContent[] calls)
    {
        ToolCalls.Enqueue(calls);
        return this;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var received = messages.ToList();
        if (received.FirstOrDefault()?.Text.StartsWith(ClaimCheck.Marker, StringComparison.Ordinal) == true)
        {
            CheckCalls.Add(received);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, CheckReply(received))) { Usage = Copy(CheckUsage) };
        }

        Calls.Add(received);
        Options.Add(options);

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Fail)
        {
            throw new HttpRequestException("simulated LLM failure");
        }

        if ((UseToolCallsWhen is null || UseToolCallsWhen(received)) && ToolCalls.TryDequeue(out var calls))
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. calls])) { Usage = Copy(Usage) };
        }

        if (FailAfterToolCalls)
        {
            throw new HttpRequestException("simulated LLM failure after the tools ran");
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply(received))) { Usage = Copy(Usage) };
    }

    /// <summary>What the model was told for this call: the system messages and the instructions in the options.</summary>
    public string InstructionsOf(int call) =>
        string.Join("\n", Calls[call].Where(m => m.Role == ChatRole.System).Select(m => m.Text).Append(Options[call]?.Instructions ?? string.Empty));

    /// <summary>The results the tools returned to the model before call <paramref name="call"/>, in order.</summary>
    public IReadOnlyList<string> ToolResultsBefore(int call) =>
        Calls[call].SelectMany(m => m.Contents.OfType<FunctionResultContent>()).Select(r => r.Result?.ToString() ?? string.Empty).ToList();

    // Each response gets its own usage object: the invocation loop adds them up.
    private static UsageDetails? Copy(UsageDetails? usage) =>
        usage is null ? null : new UsageDetails { InputTokenCount = usage.InputTokenCount, OutputTokenCount = usage.OutputTokenCount };

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The fake does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

/// <summary>Hands the same <see cref="FakeChatClient"/> out for every model and records which model each turn asked for.</summary>
public sealed class FakeChatClientProvider(FakeChatClient client) : IChatClientProvider
{
    public List<string> RequestedModels { get; } = [];

    public IChatClient Get(string modelId)
    {
        RequestedModels.Add(modelId);
        return client;
    }
}
