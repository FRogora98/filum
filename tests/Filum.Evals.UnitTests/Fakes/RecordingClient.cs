using Microsoft.Extensions.AI;

namespace Filum.Evals.UnitTests.Fakes;

internal sealed class RecordingClient : IChatClient
{
    public List<string> Prompts { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Prompts.Add(messages.Last().Text);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "4")) { Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 5 } });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
