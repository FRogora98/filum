using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.Events;

/// <summary>An event to append; the store gives it its id.</summary>
public sealed record NewMemoryEvent(
    string Kind,
    string Source,
    string Text,
    DateTimeOffset? OccurredAt = null,
    Guid? ConversationId = null,
    Guid? MessageId = null,
    string Sensitivity = MemorySensitivity.Normal,
    IReadOnlyList<long>? Sources = null,
    IReadOnlyList<long>? Revisions = null);
