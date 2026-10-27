using Microsoft.Extensions.AI;

namespace Filum.Engine.IntegrationTests.Fakes;

internal sealed class ScriptedModel : IChatClient
{
    public IList<AITool> Tools { get; private set; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Tools = options?.Tools ?? [];
        var called = messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any());
        return Task.FromResult(called
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, "Saved in /notes/plant.md."))
            : new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("c1", "memory_write", new Dictionary<string, object?> { ["path"] = "/notes/plant.md", ["content"] = "Water on Mondays.\n" })])));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
