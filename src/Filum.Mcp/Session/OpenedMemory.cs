using Filum.Engine.Abstractions.Storage;
using Filum.Engine.Packs;

namespace Filum.Mcp.Session;

// Opened once, when the server starts.
internal sealed class OpenedMemory : IDisposable
{
    private IMemoryStore? _store;

    public IMemoryStore Store => _store ?? throw new InvalidOperationException("The memory is not open: the server opens it before it starts.");

    public Guid Owner { get; private set; }

    public Pack? Pack { get; private set; }

    // The rules that held when the server started, for its instructions (spec 056); empty when none or private.
    public string Rules { get; private set; } = string.Empty;

    public void KeepRules(string rules) => Rules = rules;

    public void Open(IMemoryStore store, Guid owner, Pack? pack)
    {
        if (Interlocked.CompareExchange(ref _store, store, null) is not null)
        {
            throw new InvalidOperationException("The memory is already open.");
        }

        Owner = owner;
        Pack = pack;
    }

    public void Dispose() => (_store as IDisposable)?.Dispose();
}
