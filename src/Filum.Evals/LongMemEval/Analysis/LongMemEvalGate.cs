using Filum.Embeddings;
using System.Globalization;
using System.Text;

namespace Filum.Evals.LongMemEval.Analysis;

// No cost: the person's messages of LongMemEval S, personal sessions against the generic ones; the first questions
// train, the others test, and the messages that hold an answer set the threshold.
internal static class LongMemEvalGate
{
    // The first three fifths of the questions train; the others test.
    private const int TrainParts = 3;
    private const int Parts = 5;

    private const int MaxMessageLength = 2000;

    private const int EmbeddingBatch = 32;

    private static readonly string[] GenericSessionPrefixes = ["sharegpt", "ultrachat"];

    public static (KeepGate Gate, LongMemEvalGateReport Report) Train(IReadOnlyList<LongMemEvalInstance> instances, IEmbedder embedder, int perClass, int seed, TextWriter progress)
    {
        var split = instances.Count * TrainParts / Parts;
        var (train, _) = Messages(instances.Take(split), perClass, seed);
        var (test, evidence) = Messages(instances.Skip(split), perClass / 2, seed + 1);
        progress.WriteLine($"Embedding {train.Count} training and {test.Count + evidence.Count} test messages…");

        var trainSet = Embed(train.Select(m => m.Text).ToList(), embedder).Zip(train, (v, m) => (v, m.Keep)).ToList();
        var testVectors = Embed(test.Select(m => m.Text).ToList(), embedder);
        var evidenceVectors = Embed(evidence, embedder);
        var trainEvidence = Embed(Messages(instances.Take(split), 0, seed).Evidence, embedder);

        var gate = KeepGate.Train(trainSet, trainEvidence);
        var kept = testVectors.Select(gate.Keep).ToList();
        var accuracy = test.Select((m, i) => kept[i] == m.Keep).Average(x => x ? 1.0 : 0.0);
        var personalKept = test.Select((m, i) => (m, i)).Where(x => x.m.Keep).Average(x => kept[x.i] ? 1.0 : 0.0);
        var genericDiscarded = test.Select((m, i) => (m, i)).Where(x => !x.m.Keep).Average(x => kept[x.i] ? 0.0 : 1.0);
        var evidenceKept = evidenceVectors.Count == 0 ? double.NaN : evidenceVectors.Average(v => gate.Keep(v) ? 1.0 : 0.0);
        return (gate, new LongMemEvalGateReport(trainSet.Count, test.Count, gate.Threshold, accuracy, personalKept, genericDiscarded, evidenceVectors.Count, evidenceKept));
    }

    public static string Markdown(LongMemEvalGateReport r, string description, TimeProvider time) =>
        new StringBuilder($"# The keep gate — {time.GetUtcNow():yyyy-MM-dd HH:mm}\n\n{description}\n\n")
            .Append("| Measure (test questions) | Value |\n|---|---|\n")
            .Append(CultureInfo.InvariantCulture, $"| Training messages | {r.TrainExamples} |\n")
            .Append(CultureInfo.InvariantCulture, $"| Test messages | {r.TestExamples} |\n")
            .Append(CultureInfo.InvariantCulture, $"| Threshold | {r.Threshold:0.000} |\n")
            .Append(CultureInfo.InvariantCulture, $"| Accuracy | {r.Accuracy:P1} |\n")
            .Append(CultureInfo.InvariantCulture, $"| Personal messages kept | {r.PersonalKept:P1} |\n")
            .Append(CultureInfo.InvariantCulture, $"| Generic messages discarded | {r.GenericDiscarded:P1} |\n")
            .Append(CultureInfo.InvariantCulture, $"| **Messages holding an answer kept** | **{r.EvidenceKept:P1}** of {r.Evidence} |\n")
            .ToString();

    private static (List<(string Text, bool Keep)> Messages, List<string> Evidence) Messages(IEnumerable<LongMemEvalInstance> instances, int perClass, int seed)
    {
        var personal = new HashSet<string>(StringComparer.Ordinal);
        var generic = new HashSet<string>(StringComparer.Ordinal);
        var evidence = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in instances)
        {
            if (instance.HaystackSessionIds is not { } sessionIds)
            {
                continue;
            }

            for (var s = 0; s < instance.HaystackSessions.Count; s++)
            {
                var id = sessionIds[s];
                var isGeneric = GenericSessionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));
                foreach (var turn in instance.HaystackSessions[s].Where(t => t.Role == LongMemEvalTurn.UserRole))
                {
                    var text = turn.Content.Length > MaxMessageLength ? turn.Content[..MaxMessageLength] : turn.Content;
                    (isGeneric ? generic : personal).Add(text);
                    if (turn.HasAnswer == true)
                    {
                        evidence.Add(text);
                    }
                }
            }
        }

        personal.ExceptWith(generic);
        var random = new Random(seed);
        var sample = personal.OrderBy(_ => random.Next()).Take(perClass).Select(t => (t, true))
            .Concat(generic.OrderBy(_ => random.Next()).Take(perClass).Select(t => (t, false)))
            .ToList();
        return (sample, evidence.ToList());
    }

    private static List<float[]> Embed(IReadOnlyList<string> texts, IEmbedder embedder)
    {
        var vectors = new List<float[]>(texts.Count);
        foreach (var batch in texts.Select((t, i) => (t, i)).OrderBy(x => x.t.Length).Chunk(EmbeddingBatch))
        {
            var made = embedder.Embed(batch.Select(x => x.t).ToList(), query: false);
            vectors.AddRange(made);
        }

        // Back to the texts' order.
        var order = texts.Select((t, i) => (t, i)).OrderBy(x => x.t.Length).Select(x => x.i).ToList();
        var result = new float[texts.Count][];
        for (var k = 0; k < order.Count; k++)
        {
            result[order[k]] = vectors[k];
        }

        return [.. result];
    }
}
