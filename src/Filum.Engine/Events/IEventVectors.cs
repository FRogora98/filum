using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Events;

/// <summary>
/// The vectors of what was said, and the search of the log that uses them. Chosen in composition: with an embedding
/// model, <see cref="EventVectorService"/>; without one, <see cref="NullEventVectors"/>, which searches by words only.
/// </summary>
internal interface IEventVectors
{
    /// <summary>Makes the missing vectors, newest first, at most <paramref name="max"/>; returns how many it made.</summary>
    Task<int> MakeAsync(Guid userId, int max, CancellationToken cancellationToken);

    /// <summary>The events that best match <paramref name="query"/>, at most <paramref name="max"/>, best first.</summary>
    Task<IReadOnlyList<MemoryEventHit>> RankAsync(Guid userId, IReadOnlyList<MemoryEvent> events, string query, int max, CancellationToken cancellationToken);

    /// <summary>
    /// The cosine of <paramref name="query"/> with each text, by meaning; null without an embedding model. The texts'
    /// vectors are kept in the process, not stored: they are derived from what the memory holds.
    /// </summary>
    IReadOnlyList<float>? Similarity(string query, IReadOnlyList<string> texts);
}
