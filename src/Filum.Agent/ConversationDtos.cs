namespace Filum.Agent;

/// <param name="Unverified">The answer claims a change that no tool made, even after a second attempt.</param>
/// <param name="Proposal">A skill the answer proposes to save, and what the person decided about it.</param>
public sealed record MessageDto(Guid Id, string Role, string Content, string? Model, DateTimeOffset CreatedAt, IReadOnlyList<StepDto> Steps, bool Unverified = false, SkillProposalDto? Proposal = null);

/// <summary>A skill ready to save, attached to an answer; nothing is saved until the person accepts it.</summary>
public sealed record SkillProposalDto(string Name, string Description, string When, string Steps, string Status = SkillProposalDto.Open)
{
    public static SkillProposalDto From(SkillProposal proposal) => new(proposal.Name, proposal.Description, proposal.When, proposal.Steps);

    public const string Open = "open";
    public const string Saved = "saved";
    public const string Declined = "declined";
}

/// <summary>One thing Filum did on the person's memory while answering, as the tool reported it, never as the model told it.</summary>
public sealed record StepDto(string Kind, string Tool, string? Path, string Description, int DurationMs, long? RevisionId, string? Error = null, int? Rows = null, bool Undone = false)
{
    public static StepDto From(ToolStep step) => new(step.Kind, step.Tool, step.Path, step.Description, step.DurationMs, step.RevisionId, step.Error, step.Rows);

    public const string Read = "read";
    public const string Searched = "searched";
    public const string Listed = "listed";
    public const string Computed = "computed";
    public const string Wrote = "wrote";
    public const string Asked = "asked";
    public const string Failed = "failed";
}

public sealed record ConversationDto(Guid Id, string Title, string Model, string Preview, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SendMessageRequest(Guid Id, string? Content, string? Model = null);

public sealed record UsageDto(int InputTokens, int OutputTokens, decimal CostUsd);

public sealed record SendMessageResponse(ConversationDto Conversation, MessageDto UserMessage, MessageDto AssistantMessage, UsageDto? Usage);
