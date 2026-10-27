namespace Filum.Engine.Abstractions.Memory;

/// <summary>Where a file came from: its first revision, and the chat that made it when there was one and it still exists.</summary>
public sealed record MemoryOrigin(string Author, string? ConversationTitle, DateTimeOffset CreatedAt);
