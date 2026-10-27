using Filum.Evals.Hosts.Wire;
using Filum.Evals.Scenarios;

namespace Filum.Evals.Checks;

// On the person's own skills, not the starter ones.
internal static class SkillExpectations
{
    public static (bool, string) Skill(Expectation e, MemorySnapshot memory)
    {
        var skills = Named(e, memory).ToList();
        if (skills.Count == 0)
        {
            return (false, $"no skill named {string.Join("|", e.NameAny)} among {Names(memory)}");
        }

        var first = skills[0].Skill;
        return e.Enabled is null || skills.Any(s => s.Skill.Enabled == e.Enabled)
            ? (true, $"/{first.Name}{(first.Enabled ? "" : " (off)")}")
            : (false, $"/{first.Name} is {(first.Enabled ? "on" : "off")}");
    }

    public static (bool, string) NoSkill(Expectation e, MemorySnapshot memory) => (!Named(e, memory).Any(), $"skills: {Names(memory)}");

    // In this turn or an earlier one: the model decides when a request has repeated enough.
    public static (bool, string) Proposal(Expectation e, IReadOnlyList<TurnRecord> turns)
    {
        var proposals = turns.Select(t => t.Proposal).OfType<SkillProposalDto>().ToList();
        var named = proposals.Where(p => NameMatches(e, p.Name)).ToList();
        return named.Count > 0
            ? (true, $"proposed /{named[^1].Name}")
            : (false, proposals.Count == 0 ? "no proposal" : $"proposed {string.Join(", ", proposals.Select(p => "/" + p.Name))}");
    }

    // Any of them when no word is given.
    private static IEnumerable<(string Path, Skill Skill)> Named(Expectation e, MemorySnapshot memory) =>
        memory.OwnSkills.Where(s => NameMatches(e, s.Skill.Name));

    private static bool NameMatches(Expectation e, string name) =>
        e.NameAny.Count == 0 || e.NameAny.Any(word => ComparableText.Contains(name, word));

    private static string Names(MemorySnapshot memory) =>
        memory.OwnSkills.Count == 0 ? "(only the starter skills)" : string.Join(", ", memory.OwnSkills.Select(s => "/" + s.Skill.Name));
}
