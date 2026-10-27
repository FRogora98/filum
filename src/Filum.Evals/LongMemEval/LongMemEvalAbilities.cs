namespace Filum.Evals.LongMemEval;

// In the order the paper's reports list them.
internal static class LongMemEvalAbilities
{
    public const string Extraction = "extraction";
    public const string MultiSession = "multi-session";
    public const string KnowledgeUpdate = "knowledge-update";
    public const string Temporal = "temporal";
    public const string Abstention = "abstention";

    public static IReadOnlyList<string> All { get; } = [Extraction, MultiSession, KnowledgeUpdate, Temporal, Abstention];
}
