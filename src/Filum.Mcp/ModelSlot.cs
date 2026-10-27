using Filum.Engine.Abstractions.Search;

namespace Filum.Mcp;

// Loaded in the background, so the server answers at once and searches by words until then.
internal sealed class ModelSlot : IDisposable
{
    private IEmbedder? _current;

    // Null until loaded, or when it is off or could not load.
    public IEmbedder? Current => Volatile.Read(ref _current);

    // Once: the engine never changes its model.
    public bool TryFill(IEmbedder embedder) => Interlocked.CompareExchange(ref _current, embedder, null) is null;

    public void Dispose() => (Current as IDisposable)?.Dispose();
}
