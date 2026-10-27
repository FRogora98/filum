using Microsoft.Extensions.Logging;

namespace Filum.Engine.Memory;

// Ids and counts only: never content, paths or refusal text (LOG-03).
internal static partial class MemoryLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "A {Operation} by {Author} for user {UserId} lost a race with another change")]
    public static partial void LostRace(this ILogger logger, string operation, string author, Guid userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A file of user {UserId} changed by a failed turn could not be rolled back and is kept (revisions {RevisionIds})")]
    public static partial void RollbackKept(this ILogger logger, Guid userId, string revisionIds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "The package {Pack} could not create {Count} files or skills for user {UserId}")]
    public static partial void PackProblems(this ILogger logger, string? pack, int count, Guid userId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "The core of user {UserId} was created by a concurrent request")]
    public static partial void CoreCreatedConcurrently(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Memory created for user {UserId} ({Pack})")]
    public static partial void MemoryCreated(this ILogger logger, Guid userId, string pack);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Forgot {Events} events and {Files} files of user {UserId}")]
    public static partial void Forgot(this ILogger logger, int events, int files, Guid userId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "The facts of forgotten conversation {ConversationId} of user {UserId} could not be removed")]
    public static partial void ForgottenFactsKept(this ILogger logger, Guid userId, Guid conversationId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "User {UserId} forgot conversation {ConversationId}: {Events} events, {Files} files, {Rows} facts")]
    public static partial void ConversationForgotten(this ILogger logger, Guid userId, Guid conversationId, int events, int files, int rows);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "User {UserId} imported {New} items ({Format}, {Duplicates} duplicates, {Skipped} skipped)")]
    public static partial void Imported(this ILogger logger, Guid userId, int @new, string format, int duplicates, int skipped);
}
