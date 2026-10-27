using Filum.Engine.Abstractions.Search;

namespace Filum.Mcp.Search;

// The reranker arrives while the server runs: until then, and when it could not load, a search keeps its own order.
internal sealed class RerankerSlot : IReranker, IDisposable
{
    private IReranker? _current;

    public bool IsLoaded => Volatile.Read(ref _current) is not null;

    // Once: the server never changes its reranker.
    public bool TryFill(IReranker reranker) => Interlocked.CompareExchange(ref _current, reranker, null) is null;

    public IReadOnlyList<float>? Score(string query, IReadOnlyList<string> texts) => Volatile.Read(ref _current)?.Score(query, texts);

    public void Dispose() => (Volatile.Read(ref _current) as IDisposable)?.Dispose();
}
