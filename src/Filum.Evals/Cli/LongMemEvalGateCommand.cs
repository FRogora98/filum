using Filum.Embeddings;
using Filum.Evals.LongMemEval;
using Filum.Evals.LongMemEval.Analysis;
using System.Globalization;

namespace Filum.Evals.Cli;

// No model call, no cost.
internal static class LongMemEvalGateCommand
{
    private const int DefaultPerClass = 1500;
    private const string GateFile = "keep-gate.json";

    public static async Task<int> RunAsync(CommandLineArguments arguments, string data, LongMemEvalSubset subset, IReadOnlyList<LongMemEvalInstance> instances, TimeProvider time, CancellationToken cancellationToken)
    {
        using var model = new E5Embedder(arguments.Get(OptionNames.Embeddings) ?? throw new InvalidOperationException("--gate needs --embeddings <folder>."));
        var (gate, report) = LongMemEvalGate.Train(instances, model, arguments.Int(OptionNames.PerClass, DefaultPerClass), subset.Seed, Console.Out);
        var folder = Path.Combine(arguments.OutDirectory(), $"gate-{time.GetUtcNow().ToString(ResultsFolder.TimeFormat, CultureInfo.InvariantCulture)}");
        var text = LongMemEvalGate.Markdown(report, $"Data: {Path.GetFileName(data)}, {instances.Count} questions: the first three fifths to train, the rest to test · multilingual-e5-small vectors · logistic regression.", time);
        await ResultsFolder.WriteAsync(folder, [(ResultsFolder.ReportFile, text), (GateFile, gate.ToJson())], cancellationToken);
        Console.WriteLine(text);
        CommandOutput.Written(folder);
        return EvalsConstants.ExitCodes.Success;
    }
}
