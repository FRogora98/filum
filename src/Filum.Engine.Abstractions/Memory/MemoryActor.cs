using Filum.Engine.Abstractions.Constants;

namespace Filum.Engine.Abstractions.Memory;

/// <summary>Who makes a change: the agent in one turn, the person from the app, or the platform itself.</summary>
public sealed record MemoryActor(string Author, Guid? ConversationId = null, Guid? MessageId = null)
{
    /// <summary>The engine itself.</summary>
    public static MemoryActor Platform { get; } = new(MemoryAuthor.Platform);

    /// <summary>The person, from the app.</summary>
    public static MemoryActor Person { get; } = new(MemoryAuthor.Person);

    /// <summary>A consolidation pass.</summary>
    public static MemoryActor Consolidation { get; } = new(MemoryAuthor.Consolidation);

    /// <summary>The agent, in the turn that answers <paramref name="messageId"/> of <paramref name="conversationId"/>.</summary>
    public static MemoryActor Agent(Guid conversationId, Guid messageId) => new(MemoryAuthor.Agent, conversationId, messageId);
}
