namespace Filum.Engine.Abstractions.Memory;

/// <summary>What forgetting a conversation removed.</summary>
public sealed record ForgetResult(int Events, int Files, int FactRows);
