namespace Filum.Engine.Abstractions.Storage;

/// <summary>One change of one file, as it was saved: the whole content after the change, and who made it.</summary>
public sealed record StoredRevision(
    long Id,
    Guid FileId,
    string Path,
    string Content,
    string Sensitivity,
    bool Deleted,
    string Operation,
    string Author,
    Guid? ConversationId,
    Guid? MessageId,
    long? UndoesRevisionId,
    DateTimeOffset CreatedAt);
