namespace Filum.Evals.Hosts.Wire;

internal sealed record MessageDto(Guid Id, string Role, string Content, string? Model, DateTimeOffset CreatedAt, IReadOnlyList<StepDto> Steps, bool Unverified = false, SkillProposalDto? Proposal = null);
