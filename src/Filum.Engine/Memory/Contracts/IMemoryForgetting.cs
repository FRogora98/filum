using Filum.Engine.Abstractions.Memory;

namespace Filum.Engine.Memory.Contracts;

/// <summary>
/// Forgets for good: events, files and whole conversations, with their history. A role of <see cref="IMemoryService"/>,
/// for callers that need only this.
/// </summary>
public interface IMemoryForgetting
{
    /// <summary>Forgets for good these events and the live files at these paths, with their whole history; nothing of it can be undone.</summary>
    Task ForgetAsync(Guid userId, IReadOnlyCollection<long> eventIds, IReadOnlyCollection<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// When a conversation is deleted, what was said in it leaves the log too: its messages, the instructions and the
    /// summaries made of it. What the memory made of it stays; forgetting it is <see cref="ForgetConversationAsync"/>.
    /// </summary>
    Task<int> ForgetWhatWasSaidAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets a conversation for good: its events; the files every change of which came from it; the facts sourced only
    /// on its events; the proposals made only from it. What also came from elsewhere stays. A <c>corrected</c> event
    /// records what was forgotten, without its content.
    /// </summary>
    Task<ForgetResult> ForgetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken);
}
