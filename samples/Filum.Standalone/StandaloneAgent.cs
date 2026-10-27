using Filum.Engine.Abstractions.Events;
using Filum.Engine.Abstractions.Memory;
using Filum.Engine.Storage.LocalFolder;
using Filum.Engine;
using Microsoft.Extensions.AI;

namespace Filum.Standalone;

// The smallest agent with a Filum memory: your model (any IChatClient), the engine's tools, a folder of plain files.
public sealed class StandaloneAgent : IDisposable
{
    private const string Instructions = """
        You are a personal assistant with a memory: files in a folder that belongs to the person. Use the memory tools to
        look before you answer about them, and to write what they ask you to keep. Never claim a change you did not make.
        """;

    private readonly IChatClient _model;
    private readonly LocalFolderStore _store;
    private readonly FilumEngine _engine;
    private readonly Guid _session = Guid.NewGuid();
    private readonly List<ChatMessage> _history = [];

    public StandaloneAgent(IChatClient model, string folder)
    {
        _model = model;
        _store = new LocalFolderStore(folder);
        _engine = FilumEngine.Create(_store);
    }

    public void Dispose()
    {
        _engine.Dispose();
        _store.Dispose();
    }

    // The message is logged first; the model answers with the memory's tools at hand.
    public async Task<string> AskAsync(string message, CancellationToken cancellationToken = default)
    {
        await _engine.Memory.RecordAsync(_store.Owner, new NewMemoryEvent(MemoryEventKind.Said, MemoryEventSource.Host, message, ConversationId: _session), cancellationToken);
        var tools = _engine.Tools.Create(_store.Owner, MemoryActor.Agent(_session, Guid.NewGuid()));

        using var client = new ChatClientBuilder(_model).UseFunctionInvocation().Build();
        _history.Add(new ChatMessage(ChatRole.User, message));
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instructions), .. _history],
            new ChatOptions { Tools = tools.Tools },
            cancellationToken);
        _history.AddRange(response.Messages);
        return response.Text;
    }
}
