namespace Filum.Agent;

public sealed class ConversationMessage
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public long Sequence { get; set; }

    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public string Role { get; set; } = UserRole;

    public string Content { get; set; } = string.Empty;

    public Guid? ReplyTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? Model { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    /// <summary>The steps of an answer that used memory, as JSON; null otherwise.</summary>
    public string? StepsJson { get; set; }

    /// <summary>The answer claims a change that no tool made, even after a second attempt.</summary>
    public bool Unverified { get; set; }

    /// <summary>The skill an answer proposes, with its status, as JSON; null otherwise.</summary>
    public string? ProposalJson { get; set; }
}
