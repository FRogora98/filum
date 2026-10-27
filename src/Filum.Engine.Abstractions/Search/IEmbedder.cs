namespace Filum.Engine.Abstractions.Search;

/// <summary>
/// Turns texts into vectors whose closeness is closeness of meaning. The engine only uses one; a host or
/// <c>filum-mcp</c> gives it (<c>Filum.Embeddings</c> has a local model). Vectors are unit length, so their dot
/// product is their cosine.
/// </summary>
public interface IEmbedder
{
    /// <summary>One vector per text; <paramref name="query"/> marks texts that ask, as opposed to texts that were said.</summary>
    IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts, bool query);

    /// <summary>
    /// Which model makes the vectors, so vectors of another model are made again instead of compared (spec 039); null when
    /// it is not known, and the vectors are then taken as they are.
    /// </summary>
    string? ModelId => null;
}
