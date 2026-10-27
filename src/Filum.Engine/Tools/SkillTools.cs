using System.ComponentModel;
using Filum.Engine.Abstractions.Constants;
using Filum.Engine.Abstractions.Skills;
using Filum.Engine.Abstractions.Tools;
using Filum.Engine.Memory.Contracts;
using Filum.Engine.Skills;
using Microsoft.Extensions.AI;

namespace Filum.Engine.Tools;

internal sealed class SkillTools(MemoryToolSession session) : MemoryToolGroup(session)
{
    private IMemorySkills Skills => Session.Memory;

    public override IEnumerable<AIFunction> Functions() =>
    [
        AIFunctionFactory.Create(ListSkills, ToolNames.SkillList,
            "The person's skills: name, what, when, on or off."),
        AIFunctionFactory.Create(UseSkill, ToolNames.SkillUse,
            "The steps of one of the person's enabled skills."),
        AIFunctionFactory.Create(SaveSkill, ToolNames.SkillSave,
            "Save a skill; replace to change an existing one."),
        AIFunctionFactory.Create(ProposeSkill, ToolNames.SkillPropose,
            "Propose a skill without saving it: the person's app shows it. Save it only when they agree."),
        AIFunctionFactory.Create(SetSkillEnabled, ToolNames.SkillSetEnabled,
            "Turn a skill off or back on."),
    ];

    private Task<string> ListSkills(
        [Description("Include private skills.")] bool includePrivate = false,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.SkillList, SkillFormat.Folder, "list the skills", async () =>
        {
            var skills = await Skills.ListSkillsAsync(UserId, includePrivate, cancellationToken);
            var lines = skills
                .OrderBy(s => s.Skill.Name, StringComparer.Ordinal)
                .Select(s => $"- /{s.Skill.Name}: {s.Skill.Description} · when {s.Skill.When} · {(s.Skill.Enabled ? "on" : "off")}{(s.Sensitivity == MemorySensitivity.Private ? " · private" : string.Empty)}");
            return new ToolDone(skills.Count == 0 ? "The person has no skills yet." : string.Join('\n', lines), ToolStep.Read, SkillFormat.Folder, "Listed skills");
        });

    private Task<string> UseSkill(
        [Description("Skill name.")] string name,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.SkillUse, SkillPath(name), "use the skill", async () =>
        {
            var skill = await Skills.GetSkillAsync(UserId, Bare(name), cancellationToken);
            if (skill.Refuses(out var skillRefusal, out var found))
            {
                return skillRefusal;
            }

            if (!found.Skill.Enabled)
            {
                return $"The skill /{found.Skill.Name} is off; the person can turn it back on.";
            }

            if (found.Sensitivity == MemorySensitivity.Private)
            {
                return $"The skill /{found.Skill.Name} is private; it is used only when the person invokes it.";
            }

            return new ToolDone($"Skill /{found.Skill.Name}: {found.Skill.Description}\nFollow these steps now:\n{found.Skill.Steps}", ToolStep.Read, found.Path, $"Used skill /{found.Skill.Name}");
        });

    private Task<string> SaveSkill(
        [Description("lowercase-words-joined-by-dashes.")] string name,
        [Description("What it does, one line.")] string description,
        [Description("When it applies, one line.")] string when,
        [Description("Numbered steps.")] string steps,
        [Description("Change the existing skill.")] bool replace = false,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.SkillSave, SkillPath(name), "save the skill", async () =>
            {
                var saved = await Skills.SaveSkillAsync(UserId, Actor, new Skill(Bare(name), description, when, true, steps), replace, cancellationToken);
                if (!saved.IsRefused)
                {
                    Session.AddSavedSkill(Bare(name));
                }

                return saved;
            },
            c => $"Saved skill /{Bare(name)}",
            c => $"Saved the skill /{Bare(name)}; it is on and listed from the next message. The person can use it by typing /{Bare(name)} or by asking.", cancellationToken);

    private Task<string> ProposeSkill(
        [Description("lowercase-words-joined-by-dashes.")] string name,
        [Description("What it does, one line.")] string description,
        [Description("When it applies, one line.")] string when,
        [Description("Numbered steps.")] string steps,
        CancellationToken cancellationToken = default) =>
        Run(ToolNames.SkillPropose, SkillPath(name), "propose the skill", async () =>
        {
            var skill = new Skill(Bare(name), description, when, true, steps);
            if (SkillFormat.Validate(skill) is { } error)
            {
                return error;
            }

            if (!(await Skills.GetSkillAsync(UserId, skill.Name, cancellationToken)).IsRefused)
            {
                return $"There is already a skill named '{skill.Name}'; pick another name, or change that one with {ToolNames.SkillSave} and replace.";
            }

            Session.Propose(new SkillProposal(skill.Name, skill.Description.Trim(), skill.When.Trim(), skill.Steps.Trim()));
            return new ToolDone($"Proposed /{skill.Name}; nothing is saved. Ask the person whether to save it; if they agree, save it with {ToolNames.SkillSave}.", ToolStep.Asked, SkillFormat.PathOf(skill.Name), $"Proposed skill /{skill.Name}");
        });

    private Task<string> SetSkillEnabled(
        [Description("Skill name.")] string name,
        [Description("On or off.")] bool enabled,
        CancellationToken cancellationToken = default) =>
        Change(ToolNames.SkillSetEnabled, SkillPath(name), enabled ? "turn on the skill" : "turn off the skill",
            () => Skills.SetSkillEnabledAsync(UserId, Actor, Bare(name), enabled, cancellationToken),
            _ => $"Turned {(enabled ? "on" : "off")} /{Bare(name)}",
            _ => $"The skill /{Bare(name)} is now {(enabled ? "on" : "off")}.", cancellationToken);

    // '/log-note' and 'log-note' are the same skill.
    private static string Bare(string? name) => (name ?? string.Empty).Trim().TrimStart('/');

    private static string SkillPath(string? name) => SkillFormat.PathOf(Bare(name));
}
