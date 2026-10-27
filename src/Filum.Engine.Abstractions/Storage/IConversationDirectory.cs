namespace Filum.Engine.Abstractions.Storage;

/// <summary>The titles of a person's conversations, for a history that names the chat a change came from.</summary>
public interface IConversationDirectory
{
    /// <summary>The titles of the person's conversations among these ids; a store without conversations returns none.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ConversationTitlesAsync(Guid userId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken);
}
