using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.Events;

/// <summary>Which events to read: all of them by default, oldest first.</summary>
public sealed record EventQuery(
    long? AfterId = null,
    IReadOnlyCollection<string>? Kinds = null,
    Guid? ConversationId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? Before = null,
    bool IncludePrivate = true,
    Guid? MessageId = null)
{
    /// <summary>Whether <paramref name="e"/> is one of the events asked for; a store may filter with it.</summary>
    public bool Matches(MemoryEvent e) =>
        (AfterId is null || e.Id > AfterId)
        && (Kinds is null || Kinds.Contains(e.Kind))
        && (ConversationId is null || e.ConversationId == ConversationId)
        && (MessageId is null || e.MessageId == MessageId)
        && (From is null || e.OccurredAt >= From)
        && (Before is null || e.OccurredAt < Before)
        && (IncludePrivate || e.Sensitivity != MemorySensitivity.Private);
}
