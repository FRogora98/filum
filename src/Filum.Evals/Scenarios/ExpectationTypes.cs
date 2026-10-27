namespace Filum.Evals.Scenarios;

// As they are written in a scenario file.
internal static class ExpectationTypes
{
    public const string Wrote = "wrote";
    public const string NoWrite = "no_write";
    public const string Step = "step";
    public const string AnswerContains = "answer_contains";
    public const string AnswerNotContains = "answer_not_contains";
    public const string AnswerMaxWords = "answer_max_words";
    public const string CoreContains = "core_contains";
    public const string CoreNotContains = "core_not_contains";
    public const string MemoryContains = "memory_contains";
    public const string File = "file";
    public const string Rows = "rows";
    public const string Skill = "skill";
    public const string NoSkill = "no_skill";
    public const string UsedSkill = "used_skill";
    public const string Proposal = "proposal";
    public const string Tool = "tool";
    public const string NoTool = "no_tool";
    public const string Surfaced = "surfaced";
    public const string Refused = "refused";
    public const string HostUnchanged = "host_unchanged";
    public const string Judge = "judge";

    public const string Yes = "yes";
    public const string No = "no";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Wrote, NoWrite, Step, AnswerContains, AnswerNotContains, AnswerMaxWords,
        CoreContains, CoreNotContains, MemoryContains, File, Rows,
        Skill, NoSkill, UsedSkill, Proposal,
        Tool, NoTool, Surfaced, Refused, HostUnchanged, Judge
    };

    // A host may not map the memory these read.
    public static readonly IReadOnlySet<string> ReadingMemory = new HashSet<string>
    {
        CoreContains, CoreNotContains, MemoryContains, File, Rows, Skill, NoSkill
    };
}
