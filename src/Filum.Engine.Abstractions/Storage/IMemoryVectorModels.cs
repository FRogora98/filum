namespace Filum.Engine.Abstractions.Storage;

/// <summary>
/// Optional for a store: which embedding model made its vectors, so vectors of another model are made again instead of
/// compared with them (spec 039). A store without it keeps its vectors whatever the model.
/// </summary>
public interface IMemoryVectorModels
{
    /// <summary>The id of the model the person's vectors were made with; null when not known.</summary>
    Task<string?> VectorModelAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records <paramref name="modelId"/> as the vectors' model, dropping every vector first when <paramref name="dropVectors"/>.</summary>
    Task SetVectorModelAsync(Guid userId, string modelId, bool dropVectors, CancellationToken cancellationToken);
}
