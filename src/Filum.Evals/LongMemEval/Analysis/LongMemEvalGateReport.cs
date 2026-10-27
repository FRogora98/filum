namespace Filum.Evals.LongMemEval.Analysis;

// On the questions it did not train on.
internal sealed record LongMemEvalGateReport(int TrainExamples, int TestExamples, double Threshold, double Accuracy, double PersonalKept, double GenericDiscarded, int Evidence, double EvidenceKept);
