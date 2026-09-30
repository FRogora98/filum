namespace Filum.Agent;

/// <summary>One answer's cost. Not linked to the conversation by a foreign key: deleting a conversation does not give its cost back.</summary>
public sealed class UsageRecord
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid MessageId { get; set; }

    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal CostUsd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
