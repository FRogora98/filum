namespace Filum.Evals.Hosts.Wire;

internal sealed record SkillProposalDto(string Name, string Description, string When, string Steps, string Status = SkillProposalDto.Open)
{
    public const string Open = "open";
}
