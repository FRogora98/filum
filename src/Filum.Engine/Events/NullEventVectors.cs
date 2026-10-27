using Filum.Engine.Abstractions.Events;

namespace Filum.Engine.Events;

// Null object for a host with no embedding model: the log is searched by words.
internal sealed class NullEventVectors : IEventVectors
{
    public Task<int> MakeAsync(Guid userId, int max, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<IReadOnlyList<MemoryEventHit>> RankAsync(Guid userId, IReadOnlyList<MemoryEvent> events, string query, int max, CancellationToken cancellationToken) =>
        Task.FromResult(EventSearch.Rank(events, query, max));

    public IReadOnlyList<float>? Similarity(string query, IReadOnlyList<string> texts) => null;
}
