using Filum.Evals.Hosts.Wire;

namespace Filum.Evals.Checks;

internal sealed record TurnRecord(
    int Index,
    string Chat,
    string Message,
    string Answer,
    IReadOnlyList<StepDto> Steps,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    double LatencySeconds,
    string? Error = null,
    SkillProposalDto? Proposal = null,
    int? Status = null,
    bool ConversationExists = true,
    IReadOnlyDictionary<string, bool>? Unchanged = null)
{
    public const string UsedSkillPrefix = "Used skill ";

    // A status in the 4xx range, as the host's turn gate gives.
    public bool Refused => Status is >= EvalsConstants.Http.FirstClientError and < EvalsConstants.Http.FirstServerError;

    public bool Wrote => Steps.Any(s => s.Kind == StepDto.Wrote);

    public bool UsedSkill => Steps.Any(s => s.Kind == StepDto.Read && s.Description.StartsWith(UsedSkillPrefix, StringComparison.Ordinal));
}
