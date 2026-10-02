namespace Filum.Agent.Http;

public sealed record MemoryFileDto(string Path, string Kind, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount);

public sealed record MemoryOriginDto(string Author, string? ConversationTitle, DateTimeOffset CreatedAt);

public sealed record MemoryFileDetailDto(
    string Path, string Kind, int SizeBytes, int LineCount, string Sensitivity, DateTimeOffset UpdatedAt, string? Header, int? RowCount,
    string Content, MemoryOriginDto Origin);

public sealed record MemoryChangeDto(long RevisionId, string Path, int LineCount, int? RowCount);

public sealed record MemoryRevisionDto(long Id, string Operation, string Author, string? ConversationTitle, string Summary, DateTimeOffset CreatedAt);

public sealed record MemoryVersionDto(long Id, string Path, string Content, DateTimeOffset CreatedAt, bool Deleted);

public sealed record WriteMemoryFileRequest(string? Content);

/// <summary>What a consolidation pass did: the events it read, the changes it made, the proposals it left.</summary>
public sealed record ConsolidationResultDto(int Events, int Changes, int Proposals);

public sealed record ForgetRequest(Guid ConversationId);

/// <summary>What forgetting a conversation removed: events of the log, files, and rows of the facts.</summary>
public sealed record ForgetResultDto(int Events, int Files, int FactRows);

public sealed record SetSensitivityRequest(string? Level);

public sealed record SkillDto(string Name, string Description, string When, bool Enabled, string Path, string Sensitivity, DateTimeOffset UpdatedAt);

public sealed record ModelDto(string Id, string Name, string Description, decimal InputPricePerMillionUsd, decimal OutputPricePerMillionUsd, bool IsDefault);
