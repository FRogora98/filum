namespace Filum.Engine.Abstractions.Search;

/// <summary>
/// Scores how well each text answers a query, reading the two together (a cross-encoder), to order again the best
/// matches of a search of what was said (spec 042). Optional: a host or <c>filum-mcp</c> gives one
/// (<c>Filum.Embeddings</c> has a local model); without one, the search keeps its own order.
/// </summary>
public interface IReranker
{
    /// <summary>
    /// One score per text, higher for a better answer to <paramref name="query"/>; null when the reranker cannot score now
    /// (for example while its model loads), and the search then keeps its own order.
    /// </summary>
    IReadOnlyList<float>? Score(string query, IReadOnlyList<string> texts);
}
