using Microsoft.Extensions.Logging;

namespace Filum.Engine;

/// <summary>The person's log of events (spec 030): what was said, imported or told, and what the memory made of it.</summary>
public sealed partial class MemoryService
{
    /// <summary>Appends one event; the text is kept as it was given, line breaks normalized.</summary>
    public Task<MemoryEvent> RecordAsync(Guid userId, NewMemoryEvent next, CancellationToken cancellationToken) =>
        Store.AppendEventAsync(userId, next with { Text = Normalize(next.Text) }, DateTimeOffset.UtcNow, cancellationToken);

    public Task<IReadOnlyList<MemoryEvent>> EventsAsync(Guid userId, EventQuery query, CancellationToken cancellationToken) =>
        Store.EventsAsync(userId, query, cancellationToken);

    /// <summary>
    /// Forgets for good these events and the live files at these paths, with their whole history. Only for the person's
    /// explicit request: nothing of it can be undone.
    /// </summary>
    public async Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        var files = new List<Guid>();
        foreach (var path in paths)
        {
            if (await Store.FindLiveAsync(userId, path, cancellationToken) is { } file)
            {
                files.Add(file.Id);
            }
        }

        await Store.ForgetAsync(userId, eventIds, files, cancellationToken);
        Logger.LogInformation("[ MemoryService ] Forgot {Events} events and {Files} files of user {UserId}", eventIds.Count, files.Count, userId);
    }

    /// <summary>
    /// The events that best match <paramref name="query"/> among what was said, answered or imported, in the date window,
    /// each with the event that followed it in its conversation.
    /// </summary>
    public async Task<MemoryOutcome<IReadOnlyList<MemoryEventHit>>> SearchEventsAsync(
        Guid userId, string? query, DateTimeOffset? from, DateTimeOffset? before, bool includePrivate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
        {
            return MemoryOutcome<IReadOnlyList<MemoryEventHit>>.Refused("The query must be between 1 and 200 characters.");
        }

        var events = await Store.EventsAsync(userId, new EventQuery(Kinds: MemoryEventKind.Spoken, From: from, Before: before, IncludePrivate: includePrivate), cancellationToken);
        return MemoryOutcome<IReadOnlyList<MemoryEventHit>>.Ok(EventSearch.Rank(events, query, Limits.EventsSearchMax));
    }
}
