using Filum.Evals.LongMemEval;

namespace Filum.Evals.Cli;

// Report only: nothing is played.
internal static class LongMemEvalJournalReportCommand
{
    public static async Task<int> RunAsync(CommandLineArguments arguments, string journals, TimeProvider time, CancellationToken cancellationToken)
    {
        var all = CommandLineArguments.Split(journals).SelectMany(LongMemEvalJournal.Read).ToList();
        var merged = new LongMemEvalRunResult(all, all.Sum(r => r.CostUsd + r.JudgeCostUsd), false, all.Count, 0, arguments.Get(OptionNames.Describe, string.Empty));
        var written = await LongMemEvalReport.WriteAsync(merged, arguments.OutDirectory(), time, cancellationToken);
        Console.WriteLine(LongMemEvalReport.Markdown(merged, time));
        CommandOutput.Written(written);
        return EvalsConstants.ExitCodes.Success;
    }
}
