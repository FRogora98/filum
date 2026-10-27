namespace Filum.Engine.Abstractions.Storage;

/// <summary>The vectors of a person's events: derived data, made again when lost.</summary>
public interface IMemoryVectorStore
{
    /// <summary>Keeps the vectors of these events; vectors of events the person does not have are ignored.</summary>
    Task SaveVectorsAsync(Guid userId, IReadOnlyList<(long EventId, float[] Vector)> vectors, CancellationToken cancellationToken);

    /// <summary>The person's kept vectors, by event id.</summary>
    Task<IReadOnlyDictionary<long, float[]>> VectorsAsync(Guid userId, CancellationToken cancellationToken);
}
