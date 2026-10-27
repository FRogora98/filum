namespace Filum.Engine.Abstractions.Memory;

/// <summary>One change in a file's history, as the person sees it: who made it, when, from which conversation, and what it did.</summary>
/// <param name="Id">The revision's id, the one an undo or a restore takes.</param>
/// <param name="Path">The file's path after this change.</param>
/// <param name="Operation">What the change did (see <c>MemoryOperation</c>).</param>
/// <param name="Author">Who made it (see <c>MemoryAuthor</c>).</param>
/// <param name="ConversationId">The conversation of the turn that made it, if any.</param>
/// <param name="MessageId">The message of that turn, if any.</param>
/// <param name="Deleted">Whether the change deleted the file.</param>
/// <param name="Sensitivity">The file's sensitivity after the change.</param>
/// <param name="UndoesRevisionId">The revision an undo took back, if this is one.</param>
/// <param name="CreatedAt">When the change was saved.</param>
/// <param name="Summary">What the change did, in a few words.</param>
/// <param name="ConversationTitle">The title of the conversation, when the host knows it.</param>
public sealed record MemoryRevisionInfo(long Id, string Path, string Operation, string Author, Guid? ConversationId, Guid? MessageId, bool Deleted, string Sensitivity, long? UndoesRevisionId, DateTimeOffset CreatedAt, string Summary = "", string? ConversationTitle = null);
