namespace Filum.Evals.LongMemEval.Analysis;

internal sealed record LongMemEvalRetrievalRow(string QuestionId, string Ability, int Events, bool At5, bool At10, double Seconds, int AnswerSessions = 1, int FoundAt10 = 0);
